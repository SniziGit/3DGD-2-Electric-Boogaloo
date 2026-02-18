#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Reflection;

/// <summary>
/// Property drawer for ConditionalHideAttribute.
/// Hides the field if the condition is not met.
/// </summary>
[CustomPropertyDrawer(typeof(ConditionalHideAttribute))]
public class ConditionalHideDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        ConditionalHideAttribute condHAtt = (ConditionalHideAttribute)attribute;
        bool enabled = GetConditionalHideAttributeResult(condHAtt, property);

        bool wasEnabled = GUI.enabled;
        GUI.enabled = enabled;
        if (enabled)
        {
            EditorGUI.PropertyField(position, property, label, true);
        }
        GUI.enabled = wasEnabled;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        ConditionalHideAttribute condHAtt = (ConditionalHideAttribute)attribute;
        bool enabled = GetConditionalHideAttributeResult(condHAtt, property);

        if (enabled)
        {
            return EditorGUI.GetPropertyHeight(property, label);
        }
        else
        {
            return -EditorGUIUtility.standardVerticalSpacing;
        }
    }

    private bool GetConditionalHideAttributeResult(ConditionalHideAttribute condHAtt, SerializedProperty property)
    {
        bool enabled = false;
        string propertyPath = property.propertyPath;
        string conditionPath = propertyPath.Replace(property.name, condHAtt.conditionFieldName);
        SerializedProperty sourcePropertyValue = property.serializedObject.FindProperty(conditionPath);

        if (sourcePropertyValue != null)
        {
            object currentValue = GetValue(sourcePropertyValue);
            Debug.Log("ConditionalHide: currentValue = " + currentValue + ", type = " + currentValue.GetType() + ", conditionPath = " + conditionPath);
            foreach (object showValue in condHAtt.showValues)
            {
                Debug.Log("ConditionalHide: showValue = " + showValue + ", type = " + showValue.GetType());
                if ((int)currentValue == (int)showValue)
                {
                    enabled = true;
                    break;
                }
            }
        }
        else
        {
            Debug.LogWarning("Attempting to use a ConditionalHideAttribute but no matching SourcePropertyValue found in object: " + condHAtt.conditionFieldName + ", conditionPath = " + conditionPath);
        }

        return enabled;
    }

    private object GetValue(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                return property.intValue;
            case SerializedPropertyType.Boolean:
                return property.boolValue;
            case SerializedPropertyType.Float:
                return property.floatValue;
            case SerializedPropertyType.String:
                return property.stringValue;
            case SerializedPropertyType.Color:
                return property.colorValue;
            case SerializedPropertyType.ObjectReference:
                return property.objectReferenceValue;
            case SerializedPropertyType.LayerMask:
                return property.intValue;
            case SerializedPropertyType.Enum:
                return property.enumValueIndex;
            case SerializedPropertyType.Vector2:
                return property.vector2Value;
            case SerializedPropertyType.Vector3:
                return property.vector3Value;
            case SerializedPropertyType.Vector4:
                return property.vector4Value;
            case SerializedPropertyType.Rect:
                return property.rectValue;
            case SerializedPropertyType.ArraySize:
                return property.intValue;
            case SerializedPropertyType.Character:
                return (char)property.intValue;
            case SerializedPropertyType.AnimationCurve:
                return property.animationCurveValue;
            case SerializedPropertyType.Bounds:
                return property.boundsValue;
            case SerializedPropertyType.Gradient:
                return property.gradientValue;
            case SerializedPropertyType.Quaternion:
                return property.quaternionValue;
            default:
                return null;
        }
    }
}
#endif
