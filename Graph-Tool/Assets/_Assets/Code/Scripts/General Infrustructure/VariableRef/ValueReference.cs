using System;
using Sirenix.OdinInspector;
using UnityEngine;
using System;
using NUnit.Framework.Constraints;
using Sirenix.Serialization;
using UnityEngine.Events;
using UnityEngine.Serialization;

public class VariableReference<T> : ValueReference<T, IValueAsset<T>> {
    
    public VariableReference() : base() { }
    
    public VariableReference(T value) : base(value) { }
}

[InlineProperty]
[LabelWidth(200)]
public class ValueReference<TValue, TAsset> : IEvent<TValue> where TAsset : IValueAsset<TValue> {
    [HorizontalGroup("Reference", MaxWidth = 100)] [ValueDropdown("valueList")] [HideLabel] [SerializeField]
    protected bool useValue = true;

    [ShowIf("useValue", Animate = false)] [HorizontalGroup("Reference")] [HideLabel] [SerializeField]
    protected TValue _value;

    [HideIf("useValue", Animate = false), HorizontalGroup("Reference"),
     OnValueChanged("UpdateAsset"), HideLabel, OdinSerialize]
    protected TAsset assetReference;

    [ShowIf("@assetReference != null && useValue == false")] [LabelWidth(100)] [SerializeField]
    protected bool editAsset = false;

    [ShowIf("@assetReference != null && useValue == false")]
    [EnableIf("editAsset")]
    [FoldoutGroup("Edit", expanded: false)]
    [InlineEditor(InlineEditorObjectFieldModes.Hidden)]
    [SerializeField]
    protected TAsset _assetReference;

    public ValueReference()
    {
        useValue = true;
    }

    public ValueReference(TValue value)
    {
        useValue = true;
        _value = value;
    }
    private static ValueDropdownList<bool> valueList = new ValueDropdownList<bool>() {
        { "Reference", false },
        { "Value", true }

    };


    public TValue Value
    {
        get {
            if (useValue || assetReference == null) {
                return _value;
            }
            else {
                return assetReference.Value;
            }
        }
        set {
            if (useValue) {
                _value = value;
            }
            else {
                assetReference.Value = value;
            }
        }
    }

    public void UpdateAsset() {
        _assetReference = assetReference;
    }

    public static implicit operator TValue(ValueReference<TValue, TAsset> reference) {
        return reference.Value;
    }

    public void Raise()
    {
        if(assetReference != null && !useValue && assetReference is IEvent eventReference)
        {
            eventReference.Raise();
        }
    }

    public void RegisterListener(IListener listener)
    {
        if(assetReference != null && !useValue && assetReference is IEvent eventReference)
        {
            eventReference.RegisterListener(listener);
        }
    }

    public void DeregisterListener(IListener listener)
    {
        if(assetReference != null && !useValue && assetReference is IEvent eventReference)
        {
            eventReference.DeregisterListener(listener);
        }
    }

    public void Raise(TValue arg)
    {
        if(assetReference != null && !useValue && assetReference is IEvent<TValue> eventReference)
        {
            eventReference.Raise(arg);
        }
    }
}