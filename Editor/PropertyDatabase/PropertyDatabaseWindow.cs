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
    private const string k_PropertyDatabaseWindowAssetPath = "Packages/com.unity.search.extensions/Editor/UXML/PropertyDatabase/PropertyDatabaseWindow.uxml";
    private const string k_PropertyDatabaseListViewAssetPath = "Packages/com.unity.search.extensions/Editor/UXML/PropertyDatabase/PropertyDatabaseListView.uxml";
    private const string k_PropertyDatabaseItemAssetPath = "Packages/com.unity.search.extensions/Editor/UXML/PropertyDatabase/PropertyDatabaseItem.uxml";
    private const string k_PropertyStringListViewAssetPath = "Packages/com.unity.search.extensions/Editor/UXML/PropertyDatabase/PropertyStringListView.uxml";
    private const string k_PropertyStringItemAssetPath = "Packages/com.unity.search.extensions/Editor/UXML/PropertyDatabase/PropertyStringItem.uxml";

    private static string s_PropertyDatabaseFileName;

    private static PropertyDatabaseView s_PropertyDatabaseView;
    private static TemplateContainer s_PropertyDatabaseWindowUI;

    private static bool s_CreatedFromPropertyDatabaseView = false;

    [MenuItem("Window/Search/Property Database/Open")]
    static void Create()
    {
        var window = GetWindow<PropertyDatabaseWindow>();
        window.titleContent.text = "Property Database";
        window.Show(true);
    }

    public static PropertyDatabaseWindow CreateFromPropertyDatabaseView(PropertyDatabaseView view)
    {
        s_CreatedFromPropertyDatabaseView = true;
        s_PropertyDatabaseView = view;
        CreatePropertyDatabaseWindowUI();
        ShowPropertyDatabaseListView();
        var window = GetWindow<PropertyDatabaseWindow>();
        window.titleContent.text = "Property Database";

        return window;
    }

    public void OnEnable()
    {
        if (s_PropertyDatabaseWindowUI == null)
            CreatePropertyDatabaseWindowUI();

        rootVisualElement.Add(s_PropertyDatabaseWindowUI);
    }

    public void OnDestroy()
    {
        if (s_PropertyDatabaseWindowUI != null)
        {
            s_PropertyDatabaseWindowUI.Clear();
            s_PropertyDatabaseWindowUI = null;
        }

        s_PropertyDatabaseFileName = string.Empty;
        s_PropertyDatabaseView = default(PropertyDatabaseView);
        s_CreatedFromPropertyDatabaseView = false;
    }

    private static VisualTreeAsset LoadAssetPath(string filePath)
    {
        var UIAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(filePath);
        if (UIAsset == null)
            Debug.Log($"Could not load the { filePath } file.");

        return UIAsset;
    }

    private static void CreatePropertyDatabaseWindowUI()
    {
        var windowUIAsset = LoadAssetPath(k_PropertyDatabaseWindowAssetPath);
        if (windowUIAsset == null)
            return;

        s_PropertyDatabaseWindowUI = windowUIAsset.Instantiate(null);
        var header = s_PropertyDatabaseWindowUI.Q("Header");
        var headerText = header.Q<Label>();
        headerText.text = "Load any Property Database file to visualize its content.";

        var loadButton = s_PropertyDatabaseWindowUI.Q<Button>("LoadButton");
        var refreshButton = s_PropertyDatabaseWindowUI.Q<Button>("RefreshButton");
        loadButton.clicked += LoadPropertyDatabaseFilePath;
        refreshButton.clicked += ShowPropertyDatabaseListView;
    }

    private static void LoadPropertyDatabaseFilePath()
    {
        var initialFolder = Path.GetDirectoryName(Application.dataPath);
        var filePath = EditorUtility.OpenFilePanel("Open Property Database", initialFolder, "db");

        if (string.IsNullOrEmpty(filePath))
            return;
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

        s_CreatedFromPropertyDatabaseView = false;
        s_PropertyDatabaseView = propertyDatabase.GetView();
        s_PropertyDatabaseFileName = Path.GetFileName(filePath);
        ShowPropertyDatabaseListView();
    }

    private static void ShowPropertyDatabaseListView()
    {
        if (s_PropertyDatabaseView.Equals(default(PropertyDatabaseView)))
            return;

        if (s_PropertyDatabaseWindowUI == null)
            s_PropertyDatabaseWindowUI.Clear();

        var scrollView = new ScrollView();

        var allFileRecords = s_PropertyDatabaseView.fileStoreView.EnumerateAll().ToList();
        var allMemoryRecords = s_PropertyDatabaseView.memoryStoreView.EnumerateAll().ToList();
        var allVolatileMemoryRecords = s_PropertyDatabaseView.volatileMemoryStoreView.EnumerateAll().ToList();

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

        var listViewGroupBox = s_PropertyDatabaseWindowUI.Q("ListViewGroupBox");
        if (listViewGroupBox.childCount == 1)
            listViewGroupBox.Clear();

        scrollView.Add(fileRecordsListViewUI);
        scrollView.Add(memoryRecordsListViewUI);
        scrollView.Add(volatileMemoryListViewUI);
        scrollView.Add(stringListViewUI);

        var header = s_PropertyDatabaseWindowUI.Q("Header");
        var headerText = header.Q<Label>();
        if (!s_CreatedFromPropertyDatabaseView)
            headerText.text = "PropertyDatabase File: " + s_PropertyDatabaseFileName;
        else
            headerText.text = "Database loaded from a PropertyDatabaseView instance.";

        listViewGroupBox.Add(scrollView);
    }

    private static VisualElement ShowSpecificListView(string title, VisualTreeAsset listViewUIAsset, VisualTreeAsset listViewItemUIAsset,
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
            var stringTableView = s_PropertyDatabaseView.stringTableView;
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

    private static void SyncHeaderAndListsGeometry(GeometryChangedEvent evt, UnityEngine.UIElements.ListView listView, GroupBox listViewHeader)
    {
        if (listViewHeader == null || listView == null)
            return;

        var listScrollEnabled = listView.Q<ScrollView>().verticalScroller.enabledSelf;
        listViewHeader.style.paddingRight = listScrollEnabled ? 13 : 0;
    }

    private static void PopulatePropertyStringListView(VisualElement ve, int index, PropertyStringTableView allStrings)
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

    private static void PopulatePropetyDatabaseListView(VisualElement ve, int index, List<IPropertyDatabaseRecord> allRecords)
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

    private static VisualElement CreateRecordValueVisualElement(IPropertyDatabaseRecordValue value)
    {
        var objValue = s_PropertyDatabaseView.GetObjectFromRecordValue(value);

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
