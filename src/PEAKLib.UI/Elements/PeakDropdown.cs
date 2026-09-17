using System.Collections.Generic;
using PEAKLib.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PEAKLib.UI.Elements;

/// <summary>
/// Use <see cref="MenuAPI.CreateDropdown(string, Transform)"/>
/// </summary>
public class PeakDropdown : PeakLocalizableElement
{
    /// <summary>
    /// The dropdown component
    /// </summary>
    public TMP_Dropdown Dropdown { get; private set; }

    /// <summary>
    /// The background image component of the dropdown
    /// </summary>
    public Image Background { get; private set; }

    /// <summary>
    /// The arrow image component of the dropdown
    /// </summary>
    public Image Arrow { get; private set; }

    private void Awake()
    {
        RectTransform = GetComponent<RectTransform>();
        var dropdownTransform = transform.Find("Dropdown");

        var dropdownRect = dropdownTransform.GetComponent<RectTransform>();
        // Capture the inner rectangle before changing its parent's anchors and dimensions.
        var initialSize = dropdownRect.rect.size;

        // All public layout helpers now address this same outer rectangle.
        RectTransform.anchorMin = RectTransform.anchorMax = new Vector2(0, 1);
        RectTransform.pivot = new Vector2(0.5f, 0.5f);
        RectTransform.sizeDelta = initialSize;
        RectTransform.anchoredPosition = Vector2.zero;

        Utilities.ExpandToParent(dropdownRect);

        Dropdown = dropdownTransform.GetComponent<TMP_Dropdown>();

        Dropdown.onValueChanged.RemoveAllListeners();
        Dropdown.ClearOptions();
        base.Text = Dropdown.captionText;

        Background = dropdownTransform.GetComponent<Image>();

        var arrowtransform = dropdownTransform.Find("Arrow");
        Arrow = arrowtransform.GetComponent<Image>();
    }

    /// <summary>
    /// Sets the outer container's anchored position, exactly like the common layout helper.
    /// Retained as an instance method for existing callers compiled against PeakDropdown.
    /// </summary>
    /// <param name="position"></param>
    public PeakDropdown SetPosition(Vector2 position)
    {
        return ElementExtensions.SetPosition(this, position);
    }

    /// <summary>
    /// Set the dropdown options
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    public PeakDropdown SetOptions(List<string> options)
    {
        Dropdown.ClearOptions();
        Dropdown.AddOptions(options);

        return this;
    }

    /// <summary>
    /// Set the background color of the dropdown
    /// </summary>
    /// <param name="bgColor"></param>
    /// <returns></returns>
    public PeakDropdown SetBackgroundColor(Color bgColor)
    {
        Background.color = bgColor;
        return this;
    }

    /// <summary>
    /// Set the color of arrow in the dropdown
    /// </summary>
    /// <param name="arrowColor"></param>
    /// <returns></returns>
    public PeakDropdown SetArrowColor(Color arrowColor)
    {
        Arrow.color = arrowColor;
        return this;
    }

    /// <summary>
    /// Set the color of the Label in the dropdown
    /// </summary>
    /// <param name="labelColor"></param>
    /// <returns></returns>
    public PeakDropdown SetLabelColor(Color labelColor)
    {
        Dropdown.captionText.color = labelColor;
        return this;
    }

    /// <summary>
    /// Set the color of all selectable options in the dropdown
    /// To set an individual option's color, use RTF color tags https://docs.unity.cn/Packages/com.unity.ugui@3.0/manual/TextMeshPro/RichTextSupportedTags.html
    /// </summary>
    /// <param name="labelColor"></param>
    /// <returns></returns>
    public PeakDropdown SetOptionsColor(Color labelColor)
    {
        Dropdown.itemText.color = labelColor;
        return this;
    }

    /// <summary>
    /// Set dropdown's multiselect property
    /// (allows for selecting multiple options when enabled)
    /// See https://discussions.unity.com/t/get-the-checked-values-in-a-multi-select-tmp-dropdown/1680251/2
    /// </summary>
    /// <param name="IsMulti"></param>
    /// <returns></returns>
    public PeakDropdown SetMultiSelect(bool IsMulti)
    {
        Dropdown.MultiSelect = IsMulti;
        return this;
    }

    /// <summary>
    /// Same as Dropdown.onValueChanged.AddListener()
    /// Will return an integer corelating to the dropdown option selected
    /// </summary>
    /// <param name="onValueChangedEvent"></param>
    /// <returns></returns>
    public PeakDropdown OnValueChanged(UnityAction<int> onValueChangedEvent)
    {
        ThrowHelper.ThrowIfArgumentNull(onValueChangedEvent);

        Dropdown.onValueChanged.AddListener(onValueChangedEvent);

        return this;
    }

    /// <summary>
    /// Set initial value of dropdown
    /// Same as Dropdown.SetValueWithoutNotify
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public PeakDropdown SetInitialValue(int value)
    {
        Dropdown.SetValueWithoutNotify(value);

        return this;
    }
}
