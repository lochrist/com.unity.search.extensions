### camera_only_scene
- **Query:** `camera`
- **Description:** Find all objects with the word camera

### create_only_menu
- **Query:** `create`
- **Description:** Find all menu items with the name create

### invalid_providers
- **Query:** `t:prefab`
- **Description:** Find prefabs

### lights_in_scene
- **Query:** `t:light`
- **Description:** Find all objects of type light

### material_explorer
- **Query:** `t:material mat`
- **Description:** Find all materials
- **Table:** Name (Name:Name), _Color (#_Color:Experimental/MaterialProperty), _Parallax (#_Parallax:Experimental/MaterialProperty)

### prefabs
- **Query:** `t:prefab`
- **Description:** Find prefabs

### reset
- **Query:** ``

### se_only_settings
- **Query:** `se`

### table_anim_path
- **Query:** `select{p:animation, path}`
- **Table:** path (path), m_DurationMode (#m_DurationMode:Enum), m_Script (#m_Script:ObjectPath)

### table_asset_size
- **Query:** `select{a:assets json, path, size}`
- **Table:** path (path), size (size)

### table_default_materials
- **Query:** `*.mat`
- **Table:** Name (Name:Label), Description (Description), Value (Value)

### table_material_object_ref
- **Query:** `*.mat`
- **Table:** Name (Label:Name), m_Shader (#m_Shader:ObjectReference)

### table_materials
- **Query:** `*.mat`
- **Table:** Description (Description), _Color (#_Color:Color)

### table_scene_query_asset
- **Query:** `h:t:SceneQuery`
- **Table:** Name (Name:ItemName), m_TestReference (#m_TestReference:ObjectReference)

### table_searchcolumn_components
- **Query:** `t:searchcolumntestcomponent`
- **Table:** Enabled (enabled:GameObject/Enabled), Name (Name:Name), color (#color:Experimental/SerializedProperty), texture (#texture:Experimental/SerializedProperty), material (#material:Experimental/SerializedProperty), booleanValue (#booleanValue:Experimental/SerializedProperty), colorValue (#colorValue:Experimental/SerializedProperty), enumValue (#enumValue:Experimental/SerializedProperty), floatValue (#floatValue:Experimental/SerializedProperty), integerValue (#integerValue:Experimental/SerializedProperty), vector3 (#vector3:Experimental/SerializedProperty)

### table_test_material_prop
- **Query:** `find:test_material_prop*mat`
- **Table:** Name (Name:Name), _Color (#_Color:Experimental/MaterialProperty), _Parallax (#_Parallax:Experimental/MaterialProperty)

### table_textures
- **Query:** `select{t:texture, @label, #m_HeightScale, #m_TextureType, #m_AlphaUsage}`
- **Table:** label (label:Label), m_HeightScale (m_HeightScale), m_TextureType (m_TextureType), m_AlphaUsage (m_AlphaUsage)

### test_assets
- **Query:** `a:testassets`
- **Description:** List all indexed test assets

### textures
- **Query:** `t:texture`
- **Description:** Find textures

