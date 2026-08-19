using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MediaPipeTest.SequenceEffects.Editor
{
    [CustomEditor(typeof(SequenceEffectFactory))]
    public sealed class SequenceEffectFactoryEditor : UnityEditor.Editor
    {
        private const float VerticalGap = 2f;
        private const float ExpandedIndent = 12f;
        private const int PreviewIndentWidth = 20;

        private readonly Dictionary<string, ReorderableList> nestedLists =
            new Dictionary<string, ReorderableList>();

        private SerializedProperty brainProperty;
        private SerializedProperty restoreCameraStateProperty;
        private SerializedProperty restoreCameraBlendProperty;
        private SerializedProperty onInitializeProperty;
        private SerializedProperty onCompletedProperty;
        private SerializedProperty effectsProperty;
        private ReorderableList effectsList;
        private bool previewExpanded = true;

        private void OnEnable()
        {
            brainProperty = serializedObject.FindProperty("brain");
            restoreCameraStateProperty = serializedObject.FindProperty(
                "restoreCameraStateOnComplete");
            restoreCameraBlendProperty = serializedObject.FindProperty(
                "restoreCameraBlend");
            onInitializeProperty = serializedObject.FindProperty("onInitialize");
            onCompletedProperty = serializedObject.FindProperty("onCompleted");
            effectsProperty = serializedObject.FindProperty("effects");
            effectsList = CreateList(effectsProperty, true, "연출 요소");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(brainProperty);
            EditorGUILayout.PropertyField(restoreCameraStateProperty);
            if (restoreCameraStateProperty.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    restoreCameraBlendProperty,
                    new GUIContent("복귀 블렌드"),
                    true);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(onInitializeProperty);
            EditorGUILayout.PropertyField(onCompletedProperty);
            EditorGUILayout.Space();

            DrawSequencePreview();
            EditorGUILayout.Space();
            effectsList.DoLayoutList();

            if (serializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(target);
                Repaint();
            }
        }

        private void DrawSequencePreview()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            previewExpanded = EditorGUILayout.Foldout(
                previewExpanded,
                "연출 과정 미리보기",
                true,
                EditorStyles.foldoutHeader);

            if (previewExpanded)
            {
                EditorGUILayout.Space(2f);
                DrawSequenceDescription();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSequenceDescription()
        {
            if (effectsProperty == null || effectsProperty.arraySize == 0)
            {
                EditorGUILayout.LabelField(
                    "등록된 연출 요소가 없습니다.",
                    EditorStyles.wordWrappedLabel);
                return;
            }

            var descriptions = new List<SequencePreviewEntry>();
            AppendEffectDescriptions(effectsProperty, 0, descriptions);

            for (var i = 0; i < descriptions.Count; i++)
            {
                SequencePreviewEntry entry = descriptions[i];
                var style = new GUIStyle(EditorStyles.wordWrappedLabel);
                style.padding.left += entry.IndentLevel * PreviewIndentWidth;
                EditorGUILayout.LabelField(entry.Description, style);

                if (i < descriptions.Count - 1)
                {
                    EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
                }
            }
        }

        private static void AppendEffectDescriptions(
            SerializedProperty listProperty,
            int indentLevel,
            ICollection<SequencePreviewEntry> descriptions)
        {
            for (var i = 0; i < listProperty.arraySize; i++)
            {
                SerializedProperty element = listProperty.GetArrayElementAtIndex(i);
                try
                {
                    if (element.managedReferenceValue is RepeatEffect)
                    {
                        descriptions.Add(
                            new SequencePreviewEntry(
                                BuildRepeatDescription(element),
                                indentLevel));

                        SerializedProperty children =
                            element.FindPropertyRelative("children");
                        if (children == null || children.arraySize == 0)
                        {
                            descriptions.Add(
                                new SequencePreviewEntry(
                                    "등록된 자식 연출이 없습니다.",
                                    indentLevel + 1));
                        }
                        else
                        {
                            AppendEffectDescriptions(
                                children,
                                indentLevel + 1,
                                descriptions);
                        }
                    }
                    else
                    {
                        descriptions.Add(
                            new SequencePreviewEntry(
                                element.managedReferenceValue is SequenceEffect effect
                                ? effect.ToString()
                                : "비어 있거나 타입을 찾을 수 없는 연출 요소입니다.",
                                indentLevel));
                    }
                }
                catch (Exception exception)
                {
                    descriptions.Add(
                        new SequencePreviewEntry(
                            $"설명을 만들 수 없는 연출 요소입니다. ({exception.Message})",
                            indentLevel));
                }
            }
        }

        private static string BuildRepeatDescription(
            SerializedProperty repeatProperty)
        {
            SerializedProperty targets = repeatProperty.FindPropertyRelative("targets");
            if (targets == null || targets.arraySize == 0)
            {
                return "대상 미지정 각각에 대해 다음 과정을 반복합니다.";
            }

            var targetNames = new List<string>();
            for (var i = 0; i < targets.arraySize; i++)
            {
                UnityEngine.Object repeatTarget =
                    targets.GetArrayElementAtIndex(i).objectReferenceValue;
                targetNames.Add(
                    repeatTarget == null ? "대상 미지정" : repeatTarget.name);
            }

            return $"{string.Join(", ", targetNames)} 각각에 대해 다음 과정을 반복합니다.";
        }

        private ReorderableList CreateList(
            SerializedProperty property,
            bool allowRepeat,
            string header)
        {
            var list = new ReorderableList(
                serializedObject,
                property,
                true,
                true,
                true,
                true);

            list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, header);
            list.drawElementCallback = (rect, index, active, focused) =>
                DrawEffectElement(rect, property, index);
            list.elementHeightCallback = index =>
                GetEffectElementHeight(property, index);
            list.onAddDropdownCallback = (rect, _) =>
                ShowAddMenu(rect, property, allowRepeat, list);
            list.onRemoveCallback = _ => RemoveElement(property, list);
            list.onReorderCallback = _ => RecordListChange("Reorder Sequence Effect");
            list.drawNoneElementCallback = rect => EditorGUI.LabelField(
                rect,
                "등록된 연출 요소가 없습니다.",
                EditorStyles.centeredGreyMiniLabel);

            return list;
        }

        private void DrawEffectElement(
            Rect rect,
            SerializedProperty listProperty,
            int index)
        {
            if (index < 0 || index >= listProperty.arraySize)
            {
                return;
            }

            SerializedProperty element = listProperty.GetArrayElementAtIndex(index);
            rect.y += VerticalGap;
            rect.height = EditorGUIUtility.singleLineHeight;

            Type effectType = GetManagedReferenceType(element);
            string title = effectType == null
                ? $"{index + 1}. Missing Effect"
                : $"{index + 1}. {ObjectNames.NicifyVariableName(effectType.Name)}";

            bool expanded = EditorGUI.Foldout(
                rect,
                element.isExpanded,
                title,
                true);
            if (expanded != element.isExpanded)
            {
                element.isExpanded = expanded;
                GUI.changed = true;
            }

            if (!element.isExpanded || effectType == null)
            {
                return;
            }

            float y = rect.yMax + VerticalGap;
            float originalLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Max(120f, originalLabelWidth - 8f);

            foreach (SerializedProperty child in GetDirectChildren(element))
            {
                if (!ShouldDrawProperty(element, child))
                {
                    continue;
                }

                float childHeight;
                if (IsRepeatChildrenProperty(effectType, child))
                {
                    ReorderableList nestedList = GetNestedList(child);
                    childHeight = nestedList.GetHeight();
                    var childRect = new Rect(
                        rect.x + ExpandedIndent,
                        y,
                        rect.width - ExpandedIndent,
                        childHeight);
                    nestedList.DoList(childRect);
                }
                else
                {
                    childHeight = EditorGUI.GetPropertyHeight(child, true);
                    var childRect = new Rect(
                        rect.x + ExpandedIndent,
                        y,
                        rect.width - ExpandedIndent,
                        childHeight);
                    EditorGUI.PropertyField(childRect, child, true);
                }

                y += childHeight + VerticalGap;
            }

            EditorGUIUtility.labelWidth = originalLabelWidth;
        }

        private float GetEffectElementHeight(
            SerializedProperty listProperty,
            int index)
        {
            float height = EditorGUIUtility.singleLineHeight + VerticalGap * 2f;
            if (index < 0 || index >= listProperty.arraySize)
            {
                return height;
            }

            SerializedProperty element = listProperty.GetArrayElementAtIndex(index);
            Type effectType = GetManagedReferenceType(element);
            if (!element.isExpanded || effectType == null)
            {
                return height;
            }

            foreach (SerializedProperty child in GetDirectChildren(element))
            {
                if (!ShouldDrawProperty(element, child))
                {
                    continue;
                }

                height += IsRepeatChildrenProperty(effectType, child)
                    ? GetNestedList(child).GetHeight() + VerticalGap
                    : EditorGUI.GetPropertyHeight(child, true) + VerticalGap;
            }

            return height + VerticalGap;
        }

        private ReorderableList GetNestedList(SerializedProperty childrenProperty)
        {
            string key = childrenProperty.propertyPath;
            if (!nestedLists.TryGetValue(key, out ReorderableList list))
            {
                list = CreateList(
                    childrenProperty.Copy(),
                    false,
                    "반복 자식 요소");
                nestedLists.Add(key, list);
            }

            return list;
        }

        private void ShowAddMenu(
            Rect buttonRect,
            SerializedProperty listProperty,
            bool allowRepeat,
            ReorderableList list)
        {
            var menu = new GenericMenu();
            List<Type> effectTypes = GetEffectTypes(allowRepeat);
            if (effectTypes.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("추가 가능한 연출 요소가 없습니다."));
            }

            foreach (Type effectType in effectTypes)
            {
                Type capturedType = effectType;
                menu.AddItem(
                    new GUIContent(GetMenuLabel(effectType, effectTypes)),
                    false,
                    () => AddElement(
                        listProperty,
                        capturedType,
                        list,
                        !allowRepeat));
            }

            menu.DropDown(buttonRect);
        }

        private static List<Type> GetEffectTypes(bool allowRepeat)
        {
            return TypeCache.GetTypesDerivedFrom<SequenceEffect>()
                .Where(type => !type.IsAbstract
                               && !type.IsGenericType
                               && type.IsDefined(typeof(SerializableAttribute), false)
                               && (allowRepeat
                                   || !typeof(RepeatEffect).IsAssignableFrom(type)))
                .OrderBy(type => type.Name)
                .ThenBy(type => type.FullName)
                .ToList();
        }

        private static string GetMenuLabel(Type type, IReadOnlyCollection<Type> allTypes)
        {
            string displayName = ObjectNames.NicifyVariableName(type.Name);
            bool duplicateName = allTypes.Count(other => other.Name == type.Name) > 1;
            return duplicateName
                ? $"{displayName} ({type.Namespace})"
                : displayName;
        }

        private void AddElement(
            SerializedProperty listProperty,
            Type effectType,
            ReorderableList list,
            bool isRepeatChild)
        {
            try
            {
                Undo.RecordObject(target, "Add Sequence Effect");
                serializedObject.Update();

                int index = listProperty.arraySize;
                listProperty.arraySize++;
                SerializedProperty element = listProperty.GetArrayElementAtIndex(index);
                element.managedReferenceValue = Activator.CreateInstance(effectType);

                if (isRepeatChild)
                {
                    SerializedProperty targetSource =
                        element.FindPropertyRelative("targetSource");
                    if (targetSource != null)
                    {
                        targetSource.enumValueIndex =
                            (int)EffectTargetSource.CurrentRepeatTarget;
                    }
                }

                element.isExpanded = true;
                list.index = index;

                serializedObject.ApplyModifiedProperties();
                RecordPrefabModification();
                nestedLists.Clear();
                Repaint();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, target);
            }
        }

        private void RemoveElement(
            SerializedProperty listProperty,
            ReorderableList list)
        {
            if (list.index < 0 || list.index >= listProperty.arraySize)
            {
                return;
            }

            Undo.RecordObject(target, "Remove Sequence Effect");
            serializedObject.Update();
            listProperty.DeleteArrayElementAtIndex(list.index);
            list.index = Mathf.Clamp(
                list.index - 1,
                -1,
                listProperty.arraySize - 1);
            serializedObject.ApplyModifiedProperties();
            RecordPrefabModification();
            nestedLists.Clear();
            Repaint();
        }

        private void RecordListChange(string undoName)
        {
            Undo.RecordObject(target, undoName);
            serializedObject.ApplyModifiedProperties();
            RecordPrefabModification();
            nestedLists.Clear();
            Repaint();
        }

        private void RecordPrefabModification()
        {
            EditorUtility.SetDirty(target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        private static Type GetManagedReferenceType(SerializedProperty property)
        {
            try
            {
                return property.managedReferenceValue?.GetType();
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static bool IsRepeatChildrenProperty(
            Type effectType,
            SerializedProperty property)
        {
            return typeof(RepeatEffect).IsAssignableFrom(effectType)
                   && property.name == "children"
                   && property.isArray;
        }

        private static bool ShouldDrawProperty(
            SerializedProperty effectProperty,
            SerializedProperty childProperty)
        {
            if (childProperty.name == "explicitTarget")
            {
                SerializedProperty targetSource =
                    effectProperty.FindPropertyRelative("targetSource");
                return targetSource == null
                       || targetSource.enumValueIndex
                       != (int)EffectTargetSource.CurrentRepeatTarget;
            }

            if (childProperty.name == "customEase")
            {
                SerializedProperty zoomStyle =
                    effectProperty.FindPropertyRelative("zoomStyle");
                return zoomStyle == null
                       || zoomStyle.enumValueIndex == (int)CameraMoveStyle.Custom;
            }

            return true;
        }

        private static IEnumerable<SerializedProperty> GetDirectChildren(
            SerializedProperty property)
        {
            SerializedProperty iterator = property.Copy();
            SerializedProperty end = iterator.GetEndProperty();
            int childDepth = property.depth + 1;
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren)
                   && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                if (iterator.depth == childDepth)
                {
                    yield return iterator.Copy();
                }
            }
        }

        private readonly struct SequencePreviewEntry
        {
            public SequencePreviewEntry(string description, int indentLevel)
            {
                Description = description;
                IndentLevel = indentLevel;
            }

            public string Description { get; }

            public int IndentLevel { get; }
        }
    }
}
