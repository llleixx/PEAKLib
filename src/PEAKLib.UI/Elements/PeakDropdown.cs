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

    /// <summary>
    /// VerticalLayoutGroup is used to ensure dropdown size/position information is unaffected by other existing layouts
    /// </summary>
    public VerticalLayoutGroup VerticalLayout { get; private set; }

    private Transform? dropdownTransform;
    private RectTransform parentRect;

    private void Awake()
    {
        parentRect = gameObject.GetComponent<RectTransform>();

        dropdownTransform = transform.Find("Dropdown");
        base.RectTransform = dropdownTransform.GetComponent<RectTransform>();
        // The prefab stretches to its parent until the layout group runs. Fix the anchors now
        // so SetSize and SetPosition use the requested dimensions even before that first pass.
        RectTransform.anchorMin = RectTransform.anchorMax = new Vector2(0, 1);
        RectTransform.anchoredPosition = Vector2.zero; // we're using the parent rect positioning

        Dropdown = dropdownTransform.GetComponent<TMP_Dropdown>();

        Dropdown.onValueChanged.RemoveAllListeners();
        Dropdown.ClearOptions();
        base.Text = Dropdown.captionText;

        Background = dropdownTransform.GetComponent<Image>();

        var arrowtransform = dropdownTransform.Find("Arrow");
        Arrow = arrowtransform.GetComponent<Image>();

        // Needed to ensure dropdown size/position information is unaffected by other existing layouts
        VerticalLayout = gameObject.AddComponent<VerticalLayoutGroup>();
        VerticalLayout.childControlHeight = false;
        VerticalLayout.childControlWidth = false;
        VerticalLayout.childForceExpandHeight = false;
        VerticalLayout.childForceExpandWidth = false;
        VerticalLayout.childAlignment = TextAnchor.UpperLeft;
    }

    /// <summary>
    /// Override of PeakElement.SetPosition to correctly set the anchored position of the game object
    /// PeakElement.SetPosition will not move the correct rect transform (and likely move nothing)
    /// </summary>
    /// <param name="position"></param>
    public PeakDropdown SetPosition(Vector2 position)
    {
        // Convert the dropdown center to its wrapper's upper-left position using its actual size.
        position = new(
            position.x - RectTransform.rect.width / 2f,
            position.y + RectTransform.rect.height / 2f
        );
        parentRect.anchoredPosition = position;
        return this;
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
