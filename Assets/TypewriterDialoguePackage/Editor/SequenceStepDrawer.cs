#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(SequenceStep))]
public class SequenceStepDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        float y = position.y;
        float width = position.width;
        // Draw actionType
        SerializedProperty actionTypeProp = property.FindPropertyRelative("actionType");
        EditorGUI.PropertyField(new Rect(position.x, y, width, EditorGUIUtility.singleLineHeight), actionTypeProp);
        y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        SequenceActionType actionType = (SequenceActionType)actionTypeProp.enumValueIndex;
        switch (actionType)
        {
            case SequenceActionType.TypeText:
                DrawTypeTextSection(position.x, ref y, width, property);
                break;
            case SequenceActionType.Wait:
                DrawWaitSection(position.x, ref y, width, property);
                break;
            case SequenceActionType.WaitForInput:
                // No additional fields
                break;
            case SequenceActionType.UIAnimation:
                DrawUIAnimationSection(position.x, ref y, width, property);
                break;
            case SequenceActionType.SetActive:
                DrawSetActiveSection(position.x, ref y, width, property);
                break;
        }
        // Always draw Skip settings except for WaitForInput and SetActive
        if (actionType != SequenceActionType.WaitForInput && actionType != SequenceActionType.SetActive)
        {
            DrawSkipSection(position.x, ref y, width, property);
        }
        EditorGUI.EndProperty();
    }

    private void DrawTypeTextSection(float x, ref float y, float width, SerializedProperty property)
    {
        // Header
        EditorGUI.LabelField(new Rect(x, y, width, EditorGUIUtility.singleLineHeight), "TypeText Settings", EditorStyles.boldLabel);
        y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        // Fields
        DrawProperty(x, ref y, width, property, "targetText");
        DrawProperty(x, ref y, width, property, "textToType");
        DrawProperty(x, ref y, width, property, "typeSpeed");
        DrawProperty(x, ref y, width, property, "waitForCompletion");
    }

    private void DrawWaitSection(float x, ref float y, float width, SerializedProperty property)
    {
        EditorGUI.LabelField(new Rect(x, y, width, EditorGUIUtility.singleLineHeight), "Wait Settings", EditorStyles.boldLabel);
        y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        DrawProperty(x, ref y, width, property, "duration");
    }

    private void DrawUIAnimationSection(float x, ref float y, float width, SerializedProperty property)
    {
        EditorGUI.LabelField(new Rect(x, y, width, EditorGUIUtility.singleLineHeight), "UI Animation Settings", EditorStyles.boldLabel);
        y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        DrawProperty(x, ref y, width, property, "uiAnimationType");
        UIAnimationType animType = (UIAnimationType)property.FindPropertyRelative("uiAnimationType").enumValueIndex;
        if (animType == UIAnimationType.Scroll)
        {
            DrawProperty(x, ref y, width, property, "targetPanel");
            DrawProperty(x, ref y, width, property, "startPosition");
            DrawProperty(x, ref y, width, property, "endPosition");
        }
        else if (animType == UIAnimationType.Transform)
        {
            DrawProperty(x, ref y, width, property, "targetTransform");
            DrawProperty(x, ref y, width, property, "startTransformPosition");
            DrawProperty(x, ref y, width, property, "endTransformPosition");
        }
        // Always show
        DrawProperty(x, ref y, width, property, "animationDuration");
        DrawProperty(x, ref y, width, property, "animationCurve");
    }

    private void DrawSetActiveSection(float x, ref float y, float width, SerializedProperty property)
    {
        EditorGUI.LabelField(new Rect(x, y, width, EditorGUIUtility.singleLineHeight), "SetActive Settings", EditorStyles.boldLabel);
        y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        DrawProperty(x, ref y, width, property, "targetObject");
        DrawProperty(x, ref y, width, property, "setActiveState");
    }

    private void DrawSkipSection(float x, ref float y, float width, SerializedProperty property)
    {
        EditorGUI.LabelField(new Rect(x, y, width, EditorGUIUtility.singleLineHeight), "Skip Settings", EditorStyles.boldLabel);
        y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        DrawProperty(x, ref y, width, property, "enableSkip");
        DrawProperty(x, ref y, width, property, "skipToIndex");
    }

    private void DrawProperty(float x, ref float y, float width, SerializedProperty property, string propertyName)
    {
        SerializedProperty prop = property.FindPropertyRelative(propertyName);
        float height = EditorGUI.GetPropertyHeight(prop);
        EditorGUI.PropertyField(new Rect(x, y, width, height), prop);
        y += height + EditorGUIUtility.standardVerticalSpacing;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; // actionType
        SequenceActionType actionType = (SequenceActionType)property.FindPropertyRelative("actionType").enumValueIndex;
        switch (actionType)
        {
            case SequenceActionType.TypeText:
                height += GetSectionHeight(property, "targetText", "textToType", "typeSpeed", "waitForCompletion");
                height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; // header
                break;
            case SequenceActionType.Wait:
                height += GetSectionHeight(property, "duration");
                height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; // header
                break;
            case SequenceActionType.WaitForInput:
                // No additional
                break;
            case SequenceActionType.UIAnimation:
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("uiAnimationType")) + EditorGUIUtility.standardVerticalSpacing;
                UIAnimationType animType = (UIAnimationType)property.FindPropertyRelative("uiAnimationType").enumValueIndex;
                if (animType == UIAnimationType.Scroll)
                {
                    height += GetSectionHeight(property, "targetPanel", "startPosition", "endPosition");
                }
                else if (animType == UIAnimationType.Transform)
                {
                    height += GetSectionHeight(property, "targetTransform", "startTransformPosition", "endTransformPosition");
                }
                height += GetSectionHeight(property, "animationDuration", "animationCurve");
                height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; // header
                break;
            case SequenceActionType.SetActive:
                height += GetSectionHeight(property, "targetObject", "setActiveState");
                height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; // header
                break;
        }
        // Skip
        if (actionType != SequenceActionType.WaitForInput && actionType != SequenceActionType.SetActive)
        {
            height += GetSectionHeight(property, "enableSkip", "skipToIndex");
            height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; // header
        }
        return height;
    }

    private float GetSectionHeight(SerializedProperty property, params string[] propertyNames)
    {
        float h = 0;
        foreach (string name in propertyNames)
        {
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(name)) + EditorGUIUtility.standardVerticalSpacing;
        }
        return h;
    }
}
#endif
