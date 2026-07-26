using System;
using System.Collections;
// 큐를 안전하게 사용하기 위함
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;
using Debug = UnityEngine.Debug;

public enum ModelType
{
    Lite,
    Full,
    Heavy
}
public enum ServiceType
{
    Websocket,
    UDP
}

[Serializable]
public class ConnectData
{
    public const string Ready = "ready";
    public string type;
    public int protocolVersion;
}

public class LandMarkDataReceiver : MonoBehaviour
{
    // 수신할 로컬 포트 번호
    private string WebsocketURL => "ws://127.0.0.1:" + listenPort;
    [SerializeField] private int listenPort = 8888;
    private ClientWebSocket webSocket;
    private CancellationTokenSource cts;
    private Process serverProcess;
    private IPEndPoint udpEndpoint = new IPEndPoint(IPAddress.Any, 0);
    private readonly byte[] webSocketBuffer = new byte[64 * 1024];
    
    [SerializeField] private GeneralMapping modelMapping;
    [SerializeField] private ModelType modelType;
    [SerializeField] private ServiceType serviceType;

    // 메인 스레드로 데이터를 전달하기 위한 큐
    private readonly ConcurrentStack<string> receivedMessages = new();
    private bool isReceiving = false;
    private Thread receiveThread;
    private UdpClient udpClient;


    private IEnumerator Start()
    {
        var exePath = Path.Combine(Application.streamingAssetsPath, "PoseServer.exe");
        serverProcess = Process.Start(exePath, "--model " + modelType.ToString().ToLower() + " --transport " + serviceType.ToString().ToLower() + " --port " + listenPort);

        yield return InitializeServer();
    }

    private async Task InitializeServer()
    {
        if(serviceType == ServiceType.Websocket)
        {
            cts = new CancellationTokenSource();
            bool isConnected = false;
            while (!isConnected)
            {
                isConnected = await TryConnectToServer();
            }
            _ = WebSocketReceive();
        }
        else if(serviceType == ServiceType.UDP)
        {
            udpClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            udpEndpoint = new IPEndPoint(IPAddress.Loopback, listenPort);

            await TryConnectToServer();
            _ = UdpReceive();
        }
    }
    
    private async Task WebSocketReceive()
    {
        try
        {
            WebSocketReceiveResult receiveResult;
            string receivedMessage = string.Empty;
            while (isReceiving)
            {
                receiveResult = await webSocket.ReceiveAsync(webSocketBuffer, cts.Token);
                receivedMessage += Encoding.UTF8.GetString(webSocketBuffer, 0, receiveResult.Count);
                if (!receiveResult.EndOfMessage) 
                    continue;
                
                //Debug.Log(receivedMessage);
                receivedMessages.Push(receivedMessage);
                receivedMessage = string.Empty;
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            await TryConnectToServer();
        }
    }
    
    private async Task UdpReceive()
    {
        try
        {
            while (isReceiving)
            {
                //만약 로드하는게 사이즈가 너무 커서 한번에 다 보내거나 받을 수 없는경우 안타깝지만 뭐 송신 포멧을 바꿔서 다시해야지;; 귀찮으니 웹소켓을 그냥 쓸까용?
                var wait = await udpClient.ReceiveAsync();
                byte[] receivedBytes = wait.Buffer;
                string receivedMessage = Encoding.UTF8.GetString(receivedBytes);
                receivedMessages.Push(receivedMessage);
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }
    
    private void Update()
    {
        while (!receivedMessages.IsEmpty && receivedMessages.TryPeek(out string message)) // 이거 마지막 프레임만 받도록 하게 할라고 trypeek으로 바꿈
        {
            //Debug.Log($"메인 스레드에서 처리된 메시지: {message}");
            
            modelMapping.UpdateData(message);
            receivedMessages.Clear();
        }
    }

    private async Task<bool> TryConnectToServer()
    {
        const int retryDelayMs = 250;
        const int timeoutMs = 15000;
        
        const string json = "{\"type\":\"hello\",\"protocolVersion\":1}";

        Stopwatch timer = Stopwatch.StartNew();

        while (timer.ElapsedMilliseconds < timeoutMs)
        {
            if (serverProcess != null && serverProcess.HasExited)
            {
                Debug.LogError(
                    $"Pose Server가 시작 중 종료됐습니다: {serverProcess.ExitCode}"
                );
                return false;
            }

            try
            {
                
                bool connected = false;
                if (serviceType == ServiceType.Websocket)
                {
                    //이거 재 접속 시도 할 때마다 객체 새로 생성 안하면 오류남 한번 하고 폐기하고 한번하고 폐기하고 해야됨 귀찮넹
                    webSocket = new ClientWebSocket();
                    await webSocket.ConnectAsync(new Uri(WebsocketURL), cts.Token);
                    if (webSocket.State is WebSocketState.Closed or WebSocketState.Aborted or WebSocketState.CloseSent)
                        return false;
                    
                    //이거 확인하는 과정 귀찮은데 만약 다음에 exe만들게 된다면 생략하고 접속 된거 있으면 곧장 쏘도록 만들까?
                    await webSocket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, cts.Token);
                    connected = true;
                }
                else if (serviceType == ServiceType.UDP)
                {
                    await udpClient.SendAsync(Encoding.UTF8.GetBytes(json), json.Length, udpEndpoint);
                    var result = await udpClient.ReceiveAsync();
                    var stringData = Encoding.UTF8.GetString(result.Buffer);
                    ConnectData connectData = JsonUtility.FromJson<ConnectData>(stringData);
                    
                    if (connectData.type != ConnectData.Ready) 
                        continue;
                    
                    connected = true;
                }

                if (connected)
                {
                    isReceiving = true;
                    return true;
                }
            }
            // EXE 압축 해제 또는 포트 초기화 중일 수 있으므로 재시도
            catch (SocketException){}
            // 실패한 ClientWebSocket은 폐기하고 다음 시도에서 새로 생성
            catch (WebSocketException){}

            await Task.Delay(retryDelayMs);
        }

        Debug.LogError("Pose Server 준비 시간 초과");
        return false;
    }

    private void OnDestroy()
    {
        webSocket?.Dispose();
        cts?.Cancel();
        serverProcess?.Kill();
    }
}