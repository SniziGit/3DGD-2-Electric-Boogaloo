using System;
using UnityEngine;

/// <summary>
/// Attribute to conditionally hide a field in the Inspector based on the value of another field.
/// </summary>
[AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
public class ConditionalHideAttribute : PropertyAttribute
{
    public string conditionFieldName;
    public object[] showValues;

    public ConditionalHideAttribute(string conditionFieldName, params object[] showValues)
    {
        this.conditionFieldName = conditionFieldName;
        this.showValues = showValues;
    }
}
