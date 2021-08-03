#if USE_PROPERTY_DATABASE
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.Search;
using UnityEditor;

class PropertyDatabaseWindow : EditorWindow
{
    private const string k_ParentAssetPath = "Packages/com.unity.search.extensions/Editor/UXML/PropertyDatabase/";
    private const string k_PropertyDatabaseWindowAssetPath = k_ParentAssetPath + "PropertyDatabaseWindow.uxml";
    private const string k_PropertyDatabaseListViewAssetPath = k_ParentAssetPath + "PropertyDatabaseListView.uxml";
    private const string k_PropertyDatabaseItemAssetPath = k_ParentAssetPath + "PropertyDatabaseItem.uxml";
    private const string k_PropertyStringListViewAssetPath = k_ParentAssetPath + "PropertyStringListView.uxml";
    private const string k_PropertyStringItemAssetPath = k_ParentAssetPath + "PropertyStringItem.uxml";

    private string m_PropertyDatabaseFileName;

    private PropertyDatabaseView m_PropertyDatabaseView;
    private TemplateContainer m_PropertyDatabaseWindowUI;

    private bool m_CreatedFromPropertyDatabaseView = false;

    [MenuItem("Window/Search/Property Database/Open")]
    static void OpenWindow()
    {
        var window = CreateInstance<PropertyDatabaseWindow>();
        window.titleContent.text = "Property Database";
        window.Show();
    }

    public static PropertyDatabaseWindow CreateFromPropertyDatabaseView(PropertyDatabaseView view)
    {
        var window = CreateInstance<PropertyDatabaseWindow>();
        window.titleContent.text = "Property Database";
        window.m_CreatedFromPropertyDatabaseView = true;
        window.m_PropertyDatabaseView = view;
        window.ShowPropertyDatabaseListView();

        return window;
    }

    public void OnEnable()
    {
        if (m_PropertyDatabaseWindowUI == null)
            CreatePropertyDatabaseWindowUI();

        rootVisualElement.Add(m_PropertyDatabaseWindowUI);
    }

    public void OnDestroy()
    {
        if (m_PropertyDatabaseWindowUI != null)
        {
            m_PropertyDatabaseWindowUI.Clear();
            m_PropertyDatabaseWindowUI = null;
        }

        m_PropertyDatabaseFileName = string.Empty;
        m_PropertyDatabaseView = default(PropertyDatabaseView);
        m_CreatedFromPropertyDatabaseView = false;
    }

    private VisualTreeAsset LoadAssetPath(string filePath)
    {
        var UIAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(filePath);
        if (UIAsset == null)
            Debug.Log($"Could not load the { filePath } file.");

        return UIAsset;
    }

    private void CreatePropertyDatabaseWindowUI()
    {
        var windowUIAsset = LoadAssetPath(k_PropertyDatabaseWindowAssetPath);
        if (windowUIAsset == null)
            return;

        m_PropertyDatabaseWindowUI = windowUIAsset.Instantiate(null);
        var header = m_PropertyDatabaseWindowUI.Q("Header");
        var headerText = header.Q<Label>();
        headerText.text = "Load any Property Database file to visualize its content.";

        var loadButton = m_PropertyDatabaseWindowUI.Q<Button>("LoadButton");
        var refreshButton = m_PropertyDatabaseWindowUI.Q<Button>("RefreshButton");
        loadButton.clicked += LoadPropertyDatabaseFilePath;
        refreshButton.clicked += ShowPropertyDatabaseListView;
    }

    private void LoadPropertyDatabaseFilePath()
    {
        var initialFolder = Path.GetDirectoryName(Application.dataPath);
        var filePath = EditorUtility.OpenFilePanel("Open Property Database", initialFolder, "db");

        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }
        else if (!File.Exists(filePath + ".st"))
        {
            Debug.Log($"The file you are trying to load { filePath } is not a PropertyDatabase.");
            return;
        }

        var propertyDatabase = new PropertyDatabase(filePath, true);
        if (propertyDatabase == null)
        {
            Debug.Log($"Could not load the { filePath } file.");
            return;
        }

        m_CreatedFromPropertyDatabaseView = false;
        m_PropertyDatabaseView = propertyDatabase.GetView();
        m_PropertyDatabaseFileName = Path.GetFileName(filePath);
        ShowPropertyDatabaseListView();
    }

    private void ShowPropertyDatabaseListView()
    {
        if (m_PropertyDatabaseView.Equals(default(PropertyDatabaseView)))
            return;

        if (m_PropertyDatabaseWindowUI == null)
            return;

        var scrollView = new ScrollView();

        var allFileRecords = m_PropertyDatabaseView.fileStoreView.EnumerateAll().ToList();
        var allMemoryRecords = m_PropertyDatabaseView.memoryStoreView.EnumerateAll().ToList();
        var allVolatileMemoryRecords = m_PropertyDatabaseView.volatileMemoryStoreView.EnumerateAll().ToList();

        var listViewItemUIAsset = LoadAssetPath(k_PropertyDatabaseItemAssetPath);
        var listViewUIAsset = LoadAssetPath(k_PropertyDatabaseListViewAssetPath);

        if (listViewItemUIAsset == null || listViewUIAsset == null)
            return;

        var fileRecordsListViewUI = ShowSpecificListView("File Records", listViewUIAsset, listViewItemUIAsset, false, allFileRecords);
        var memoryRecordsListViewUI = ShowSpecificListView("Memory Records", listViewUIAsset, listViewItemUIAsset, false, allMemoryRecords);
        var volatileMemoryListViewUI = ShowSpecificListView("Volatile Memory Records", listViewUIAsset, listViewItemUIAsset, false, allVolatileMemoryRecords);

        var stringListViewItemUIAsset = LoadAssetPath(k_PropertyStringItemAssetPath);
        var stringListViewUIAsset = LoadAssetPath(k_PropertyStringListViewAssetPath);

        if (stringListViewItemUIAsset == null || stringListViewUIAsset == null)
            return;

        var stringListViewUI = ShowSpecificListView("String Table", stringListViewUIAsset, stringListViewItemUIAsset, true);

        var listViewGroupBox = m_PropertyDatabaseWindowUI.Q("ListViewGroupBox");
        if (listViewGroupBox.childCount >= 1)
            listViewGroupBox.Clear();

        scrollView.Add(fileRecordsListViewUI);
        scrollView.Add(memoryRecordsListViewUI);
        scrollView.Add(volatileMemoryListViewUI);
        scrollView.Add(stringListViewUI);

        var header = m_PropertyDatabaseWindowUI.Q("Header");
        var headerText = header.Q<Label>();
        if (!m_CreatedFromPropertyDatabaseView)
            headerText.text = "PropertyDatabase File: " + m_PropertyDatabaseFileName;
        else
            headerText.text = "Database loaded from a PropertyDatabaseView instance.";

        listViewGroupBox.Add(scrollView);
    }

    private VisualElement ShowSpecificListView(string title, VisualTreeAsset listViewUIAsset, VisualTreeAsset listViewItemUIAsset,
        bool isStringListView, List<IPropertyDatabaseRecord> allRecords = null)
    {
        if (!isStringListView && allRecords == null)
            Debug.Log("List of records should not be null.");

        var listViewUI = listViewUIAsset.Instantiate(null);
        var listViewTitle = listViewUI.Q<Label>();
        listViewTitle.text = title;

        var listView = listViewUI.Q<UnityEngine.UIElements.ListView>();
        listView.makeItem = () => listViewItemUIAsset.Instantiate(null);

        var listViewHeader = listViewUI.Q<GroupBox>("ListViewHeader");
        var scrollView = listView.Q<ScrollView>();
        scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scrollView.RegisterCallback<GeometryChangedEvent>((evt) => SyncHeaderAndListsGeometry(evt, listView, listViewHeader));

        if (isStringListView)
        {
            var stringTableView = m_PropertyDatabaseView.stringTableView;
            var allStringsFromTable = stringTableView.GetAllStrings();

            if (allStringsFromTable.ToList().Count == 0)
                listViewTitle.text += " (Empty)";

            listView.bindItem = (ve, index) => PopulatePropertyStringListView(ve, index, stringTableView);
            listView.itemsSource = allStringsFromTable.ToList();
        }
        else
        {
            if (allRecords.Count == 0)
                listViewTitle.text += " (Empty)";

            listView.bindItem = (ve, index) => PopulatePropetyDatabaseListView(ve, index, allRecords);
            listView.itemsSource = allRecords;
        }

        return listViewUI;
    }

    private void SyncHeaderAndListsGeometry(GeometryChangedEvent evt, UnityEngine.UIElements.ListView listView, GroupBox listViewHeader)
    {
        if (listViewHeader == null || listView == null)
            return;

        var listScrollEnabled = listView.Q<ScrollView>().verticalScroller.enabledSelf;
        listViewHeader.style.paddingRight = listScrollEnabled ? 13 : 0;
    }

    private void PopulatePropertyStringListView(VisualElement ve, int index, PropertyStringTableView allStrings)
    {
        var allStringsFromTable = allStrings.GetAllStrings();

        var recordInfoTextField = ve.Query<TextField>().ToList();
        foreach (var textField in recordInfoTextField)
        {
            if (textField.name == "Symbol")
                textField.value = allStrings.ToSymbol(allStringsFromTable[index]).ToString();
            else if (textField.name == "StringValue")
                textField.value = allStringsFromTable[index];
        }
    }

    private void PopulatePropetyDatabaseListView(VisualElement ve, int index, List<IPropertyDatabaseRecord> allRecords)
    {
        var recordInfoToggle = ve.Q<Toggle>("RecordValid");
        recordInfoToggle.value = allRecords[index].validRecord;
        recordInfoToggle.SetEnabled(false);

        var recordInfoTextField = ve.Query<TextField>().ToList();
        foreach (var textField in recordInfoTextField)
        {
            if (textField.name == "DocumentKey")
                textField.value = allRecords[index].key.documentKey.ToString();
            else if (textField.name == "PropertyKey")
                textField.value = allRecords[index].key.propertyKey.ToString();
            else if (textField.name == "RecordType")
                textField.value = allRecords[index].value.type.ToString();
        }

        var recordValueVisualElement = CreateRecordValueVisualElement(allRecords[index].value);
        var recordValueColumn = ve.Q("RecordValue");

        if (recordValueColumn.childCount >= 1)
            recordValueColumn.Clear();

        recordValueColumn.Add(recordValueVisualElement);
    }

    private VisualElement CreateRecordValueVisualElement(IPropertyDatabaseRecordValue value)
    {
        var objValue = m_PropertyDatabaseView.GetObjectFromRecordValue(value);

        switch (value.type)
        {
            case PropertyDatabaseType.UnsignedInteger:
                var uintField = new IntegerField();
                uintField.isReadOnly = true;
                uintField.value = (int)(uint)objValue;
                return uintField;
            case PropertyDatabaseType.Integer:
                var intField = new IntegerField();
                intField.isReadOnly = true;
                intField.value = (int)objValue;
                return intField;
            case PropertyDatabaseType.Short:
                var shortField = new IntegerField();
                shortField.isReadOnly = true;
                shortField.value = (short)objValue;
                return shortField;
            case PropertyDatabaseType.UnsignedShort:
                var ushortField = new IntegerField();
                ushortField.isReadOnly = true;
                ushortField.value = (ushort)objValue;
                return ushortField;
            case PropertyDatabaseType.UnsignedLong:
                var ulongField = new LongField();
                ulongField.isReadOnly = true;
                ulongField.value = (long)(ulong)objValue;
                return ulongField;
            case PropertyDatabaseType.Long:
                var longField = new LongField();
                longField.isReadOnly = true;
                longField.value = (long)objValue;
                return longField;
            case PropertyDatabaseType.Float:
                var floatField = new FloatField();
                floatField.isReadOnly = true;
                floatField.value = (float)objValue;
                return floatField;
            case PropertyDatabaseType.Double:
                var doubleField = new DoubleField();
                doubleField.isReadOnly = true;
                doubleField.value = (double)objValue;
                return doubleField;
            case PropertyDatabaseType.Color:
                var colorField = new ColorField();
                colorField.SetEnabled(false);
                colorField.value = (Color)objValue;
                return colorField;
            case PropertyDatabaseType.Color32:
                var color32Field = new ColorField();
                color32Field.SetEnabled(false);
                color32Field.value = (Color32)objValue;
                return color32Field;
            case PropertyDatabaseType.Bool:
                var toggle = new Toggle();
                toggle.SetEnabled(false);
                toggle.value = (bool)objValue;
                return toggle;
            case PropertyDatabaseType.Vector4:
                var vector4Field = new Vector4Field();
                vector4Field.SetEnabled(false);
                vector4Field.value = (Vector4)objValue;
                return vector4Field;
            case PropertyDatabaseType.FixedString:
            case PropertyDatabaseType.String:
            case PropertyDatabaseType.GlobalObjectId:
            case PropertyDatabaseType.InstanceId:
            case PropertyDatabaseType.GameObjectProperty:
            case PropertyDatabaseType.Volatile:
            case PropertyDatabaseType.Byte:
                var textField = new TextField();
                textField.isReadOnly = true;
                textField.value = objValue.ToString();
                return textField;
            default:
                return null;
        }
    }
}
#endif
