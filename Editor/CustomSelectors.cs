using UnityEngine;
using UnityEditor.Search;
using UnityEditor;

static class CustomSelectors
{
	#if USE_SEARCH_TABLE
	
	[SearchSelector("vertices", provider: "scene")]
	static object SelectVertices(SearchSelectorArgs args)
	{
		var go = args.current.ToObject<GameObject>();
		if (!go)
			return null;

		var meshFilter = go.GetComponent<MeshFilter>();
		if (!meshFilter || !meshFilter.sharedMesh)
			return null;

		return meshFilter.sharedMesh.vertexCount;
	}

	[SearchSelector("property_count", provider: "asset")]
	static object SelectPropertyCount(SearchSelectorArgs args)
	{
		Shader shader = args.current.ToObject<Shader>();
		if (!shader && args.current.ToObject() is Material material)
			shader = material.shader;

		if (shader)
			return shader.GetPropertyCount();

		return 0;		
	}

	[SearchSelector("loc", provider: "asset")]
	static object SelectLineOfCode(SearchSelectorArgs args)
	{
		TextAsset textAsset = args.current.ToObject<TextAsset>();
		if (textAsset)
			return textAsset.text.Split('\n').Length;

		return null;
	}

	#else
	[SearchExpressionSelector("^vertices$", provider: "scene")]
	static object SelectVertices(SearchExpressionSelectorArgs args)
	{
		var go = args.current.ToObject<GameObject>();
		if (!go)
			return null;

		var meshFilter = go.GetComponent<MeshFilter>();
		if (!meshFilter || !meshFilter.sharedMesh)
			return null;

		return meshFilter.sharedMesh.vertexCount;
	}

	[SearchExpressionSelector("^path", provider: "scene")]
	static object SelectPath(SearchExpressionSelectorArgs args)
	{
		var go = args.current.ToObject<GameObject>();
		if (!go)
			return null;

		return UnityEditor.Search.SearchUtils.GetHierarchyPath(go, false);
	}

	[SearchExpressionSelector(@"~?(?<propertyPath>[\w\d\.]+)", priority: 9999, provider: "scene")]
	static object SelectComponentProperty(SearchExpressionSelectorArgs args)
	{
		if (!(args["propertyPath"] is string propertyPath))
			return null;

		var item = args.current;
		var go = item.ToObject<GameObject>();
		if (!go)
			return null;

		bool debugInfo = args.current.context?.options.HasFlag(SearchFlags.Debug) ?? false;

		foreach (var c in go.GetComponents<Component>())
		{
			using (var so = new SerializedObject(c))
			{
				if (so.FindProperty(propertyPath) is SerializedProperty sp)
					return GetSerializedPropertyValue(sp);

				using (var property = so.GetIterator())
				{
					var next = property.NextVisible(true);
					while (next)
					{
						if (debugInfo)
							Debug.Log($"{propertyPath} > {property.name} > {property.propertyPath} ({property.propertyType})");
						if (property.name.EndsWith(propertyPath, System.StringComparison.OrdinalIgnoreCase))
							return GetSerializedPropertyValue(property);
						if (property.propertyPath.Replace("m_", "").EndsWith(propertyPath, System.StringComparison.OrdinalIgnoreCase))
							return GetSerializedPropertyValue(property);
						next = property.NextVisible(property.hasChildren);
					}
				}
			}
		}
		return null;
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Correctness", "UNT0008:Null propagation on Unity objects", Justification = "<Pending>")]
	static object GetSerializedPropertyValue(SerializedProperty p)
	{
		switch (p.propertyType)
		{
			case SerializedPropertyType.Integer:
				return p.intValue;
			case SerializedPropertyType.Boolean:
				return p.boolValue;
			case SerializedPropertyType.Float:
				return p.floatValue;
			case SerializedPropertyType.String:
				return p.stringValue;
			case SerializedPropertyType.Enum:
				return p.enumNames[p.enumValueIndex];
			case SerializedPropertyType.Bounds:
				return p.boundsValue.size.magnitude;
			case SerializedPropertyType.BoundsInt:
				return p.boundsIntValue.size.magnitude;
			case SerializedPropertyType.Color:
				return p.colorValue;
			case SerializedPropertyType.LayerMask:
				return p.layerMaskBits;
			case SerializedPropertyType.FixedBufferSize:
				return p.fixedBufferSize;
			case SerializedPropertyType.ArraySize:
				return p.arraySize;

			case SerializedPropertyType.Rect:
				return p.rectValue.ToString();
			case SerializedPropertyType.RectInt:
				return p.rectIntValue.ToString();

			case SerializedPropertyType.Vector2:
				return p.vector2Value.ToString();
			case SerializedPropertyType.Vector3:
				return p.vector3Value.ToString();
			case SerializedPropertyType.Vector4:
				return p.vector4Value.ToString();
			case SerializedPropertyType.AnimationCurve:
				return p.animationCurveValue.ToString();
			case SerializedPropertyType.Gradient:
				return p.gradientValue.ToString();
			case SerializedPropertyType.Quaternion:
				return p.quaternionValue.eulerAngles.ToString();
			case SerializedPropertyType.Vector2Int:
				return p.vector2IntValue.ToString();
			case SerializedPropertyType.Vector3Int:
				return p.vector3IntValue.ToString();
			case SerializedPropertyType.Hash128:
				return p.hash128Value.ToString();

			case SerializedPropertyType.ObjectReference:
				return p.objectReferenceValue?.name;
			case SerializedPropertyType.ExposedReference:
				return p.exposedReferenceValue?.name;

			case SerializedPropertyType.Generic:
				return p.arraySize;

			case SerializedPropertyType.ManagedReference:
			case SerializedPropertyType.Character:
				break;
		}

		return null;
	}
#endif
}
