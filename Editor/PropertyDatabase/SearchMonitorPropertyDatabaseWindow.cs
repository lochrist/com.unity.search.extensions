#if !USE_SEARCH_MODULE
using UnityEditor.Experimental.SceneManagement;
#endif

using UnityEditor.Search;
using UnityEditor;

class SearchMonitorPropertyDatabaseWindow : EditorWindow
{
    [MenuItem("Window/Search/Property Database/Search Monitor")]
    static void Create()
    {
        var view = SearchMonitor.GetView().propertyDatabaseView;
        var window = PropertyDatabaseWindow.CreateFromPropertyDatabaseView(view);
        window.Show(true);
    }
}