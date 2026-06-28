// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

#if AMPLIFY_SHADER_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AmplifyShaderEditor
{
	public static class ASEHelper
	{
		public static class RangedFloatNode
		{
			public static void SetMinMax( AmplifyShaderEditor.PropertyNode prop, float min, float max )
			{
				prop.GetType().GetField( "m_min", BindingFlags.Instance | BindingFlags.NonPublic ).SetValue( prop, min );
				prop.GetType().GetField( "m_max", BindingFlags.Instance | BindingFlags.NonPublic ).SetValue( prop, max );
			}
		}

		public static class PropertyNode
		{
			public static void SetInspectorName( AmplifyShaderEditor.PropertyNode prop, string name )
			{
				prop.GetType().GetField( "m_propertyInspectorName", BindingFlags.Instance | BindingFlags.NonPublic ).SetValue( prop, name );
			}

			public static void AddAttribute( AmplifyShaderEditor.PropertyNode prop, int attr )
			{
				( prop.GetType().GetField( "m_selectedAttribs", BindingFlags.Instance | BindingFlags.NonPublic ).GetValue( prop ) as List<int> ).Add( attr );
			}
		}
	}

	[Serializable]
	[NodeAttributes( "Amplify Impostor", "Amplify Impostor Runtime", "Amplify Impostor node for runtime shaders.", CustomCategoryColor = "#7d5299", NodeAvailabilityFlags = ( int )NodeAvailability.TemplateShader )]
	public sealed class AmplifyImpostorNode : ParentNode
	{
		private static readonly string[] ImpostorOutputStructDeclaration = new string[] {
			"struct ImpostorOutput",
			"{",
			"	half3 Albedo;",
			"	half3 Specular;",
			"	half Metallic;",
			"	half3 WorldNormal;",
			"	half Smoothness;",
			"	half Occlusion;",
			"	half3 Emission;",
			"	half Alpha;",
			"};" };

		private string m_functionHeaderSphereFrag = "SphereImpostorFragment( io, {0}, {1}, {2}, {3} )";
		private string m_functionHeaderSphere = "SphereImpostorVertex( {0}, {1}, {2}, {3}, {4} )";
		private string m_functionHeaderFrag = "OctaImpostorFragment( io, {0}, {1}, {2}, {3}, {4}, {5}, {6} )";
		private string m_functionHeader = "OctaImpostorVertex( {0}, {1}, {2}, {3}, {4}, {5}, {6}, {7} )";
		private string m_functionBody = string.Empty;

		private enum CustomImpostorType
		{
			Spherical = 0,
			Octahedron = 1,
			HemiOctahedron = 2,
			CombinedOctahedron = 3
		}

		private const string WorkflowStr = "Workflow";
		private string[] DielecticSRPFix =
		{
			"#ifdef UNITY_COLORSPACE_GAMMA//AI_SRP",
			"#define unity_ColorSpaceDielectricSpec half4(0.220916301, 0.220916301, 0.220916301, 1.0 - 0.220916301)//AI_SRP",
			"#else//AI_SRP",
			"#define unity_ColorSpaceDielectricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) //AI_SRP",
			"#endif//AI_SRP"
		};

		struct PropertyNames
		{
			public string unique;
			public string legacy;
			public string inspector;
			public PropertyNames( string unique, string legacy, string inspector )
			{
				this.unique = unique;
				this.legacy = legacy;
				this.inspector = inspector;
			}
		}

		struct PropertyEntry
		{
			public PropertyNode prop;
			public PropertyNames names;
			public PropertyEntry( PropertyNode prop, PropertyNames names )
			{
				this.prop = prop;
				this.names = names;
			}
		}

		Dictionary<string, PropertyEntry> m_propertyNodes = new Dictionary<string, PropertyEntry>();

		[SerializeField] private ASEStandardSurfaceWorkflow m_workflow = ASEStandardSurfaceWorkflow.Specular;
		[SerializeField] private CustomImpostorType m_customImpostorType = CustomImpostorType.Octahedron;

		private bool IsSpherical => m_customImpostorType == CustomImpostorType.Spherical;
		private bool IsOctahedron => m_customImpostorType == CustomImpostorType.Octahedron;
		private bool IsHemiOctahedron => m_customImpostorType == CustomImpostorType.HemiOctahedron;
		private bool IsCombinedOctahedron => m_customImpostorType == CustomImpostorType.CombinedOctahedron;
		private bool IsAnyOctahedron => IsOctahedron || IsHemiOctahedron || IsCombinedOctahedron;

		[SerializeField] private bool m_hemiToggleSupport;
		[SerializeField] private bool m_parallaxSupport;
		[SerializeField] private bool m_speedTreeHueSupport;
		[SerializeField] private bool m_frameClampSupport;
		[SerializeField] private bool m_positionTextureSupport;
		[SerializeField] private bool m_showExtraData;
		[SerializeField] private int m_extraSamplers = 0;

		private const int MaxExtraSamplers = 8;
		private string[] m_extraPropertyNames;
		private InputPort m_samplerStatePort;
		[SerializeField] private bool m_matchPropertyNames = false;

		[SerializeField] private TexturePropertyNode m_albedoTexture;
		[SerializeField] private TexturePropertyNode m_normalTexture;
		[SerializeField] private TexturePropertyNode m_specularTexture;
		[SerializeField] private TexturePropertyNode m_occlusionTexture;
		[SerializeField] private TexturePropertyNode m_emissionTexture;
		[SerializeField] private TexturePropertyNode m_positionTexture;
		[SerializeField] private RangedFloatNode m_clipProp;
		[SerializeField] private RangedFloatNode m_textureBiasProp;
		[SerializeField] private RangedFloatNode m_useParallaxProp;
		[SerializeField] private RangedFloatNode m_parallaxProp;
		[SerializeField] private RangedFloatNode m_shadowBiasProp;
		[SerializeField] private RangedFloatNode m_shadowViewProp;
		[SerializeField] private RangedFloatNode m_forwardBiasProp;
		[SerializeField] private RangedFloatNode m_hemiProp;
		[SerializeField] private RangedFloatNode m_useHueProp;
		[SerializeField] private ColorNode m_hueProp;
		[SerializeField] private RangedFloatNode m_clipNeighborsProp;
		[SerializeField] private RangedFloatNode m_framesProp;
		[SerializeField] private RangedFloatNode m_framesXProp;
		[SerializeField] private RangedFloatNode m_framesYProp;
		[SerializeField] private RangedFloatNode m_depthProp;
		[SerializeField] private RangedFloatNode m_impostorSizeProp;
		[SerializeField] private Vector3Node m_offsetProp;
		[SerializeField] private Vector4Node m_sizeOffsetProp;
		[SerializeField] private Vector3Node m_boundsMinProp;
		[SerializeField] private Vector3Node m_boundsSizeProp;

		private int m_orderAlbedoTexture = 0;
		private int m_orderNormalTexture = 1;
		private int m_orderSpecularTexture = 2;
		private int m_orderOcclusionTexture = 3;
		private int m_orderEmissionTexture = 4;
		private int m_orderPositionTexture = 5;
		private int m_orderClipProp = 6;
		private int m_orderTextureBiasProp = 7;
		private int m_orderUseParallaxProp = 8;
		private int m_orderParallaxProp = 9;
		private int m_orderShadowBiasProp = 10;
		private int m_orderShadowViewProp = 11;
		private int m_orderForwardBiasProp = 12;
		private int m_orderHemiProp = 13;
		private int m_orderUseHueProp = 14;
		private int m_orderHueProp = 15;
		private int m_orderClipNeighbors = 16;
		private int m_orderFramesProp = 17;
		private int m_orderFramesXProp = 18;
		private int m_orderFramesYProp = 19;
		private int m_orderDepthProp = 20;
		private int m_orderImpostorSizeProp = 21;
		private int m_orderOffsetProp = 22;
		private int m_orderSizeOffsetProp = 23;
		private int m_orderBoundsMinProp = 24;
		private int m_orderBoundsSizeProp = 25;

		private static readonly PropertyNames m_albedoTextureNames = new PropertyNames( "_Albedo", null, "Albedo (RGB) Alpha (A)" );
		private static readonly PropertyNames m_normalTextureNames = new PropertyNames( "_Normals", null, "Normals (RGB) Depth (A)" );
		private static readonly PropertyNames m_specularTextureNames = new PropertyNames( "_Specular", null, "Specular (RGB) Smoothness (A)" );
		private static readonly PropertyNames m_occlusionTextureNames = new PropertyNames( "_Occlusion", null, "Occlusion (RGB)" );
		private static readonly PropertyNames m_emissionTextureNames = new PropertyNames( "_Emission", null, "Emission (RGB)" );
		private static readonly PropertyNames m_positionTextureNames = new PropertyNames( "_Position", null, "Position (RGB)" );
		private static readonly PropertyNames m_clipNames = new PropertyNames( "_AI_Clip", "_ClipMask", "Clip" );
		private static readonly PropertyNames m_textureBiasNames = new PropertyNames( "_AI_TextureBias", "_TextureBias", "Texture Bias" );
		private static readonly PropertyNames m_useParallaxNames = new PropertyNames( "_AI_Use_Parallax", "_Use_Parallax", "Use Parallax" );
		private static readonly PropertyNames m_parallaxNames = new PropertyNames( "_AI_Parallax", "_Parallax", "Parallax" );
		private static readonly PropertyNames m_shadowBiasNames = new PropertyNames( "_AI_ShadowBias", null, "Shadow Bias" );
		private static readonly PropertyNames m_shadowViewNames = new PropertyNames( "_AI_ShadowView", null, "Shadow View" );
		private static readonly PropertyNames m_forwardBiasNames = new PropertyNames( "_AI_ForwardBias", null, "Forward Bias" );
		private static readonly PropertyNames m_hemiNames = new PropertyNames( "_AI_Hemi", "_Hemi", "Hemi" );
		private static readonly PropertyNames m_useHueNames = new PropertyNames( "_AI_Hue", "_Hue", "Use SpeedTree Hue" );
		private static readonly PropertyNames m_hueNames = new PropertyNames( "_AI_HueVariation", "_HueVariation", "Hue Variation" );
		private static readonly PropertyNames m_clipNeighborsNames = new PropertyNames( "_AI_ClipNeighborsFrames", "AI_CLIP_NEIGHBOURS_FRAMES", "Clip Neighbours Frames" );
		private static readonly PropertyNames m_framesNames = new PropertyNames( "_AI_Frames", "_Frames", "Frames" );
		private static readonly PropertyNames m_framesXNames = new PropertyNames( "_AI_FramesX", "_FramesX", "Frames X" );
		private static readonly PropertyNames m_framesYNames = new PropertyNames( "_AI_FramesY", "_FramesY", "Frames Y" );
		private static readonly PropertyNames m_depthNames = new PropertyNames( "_AI_DepthSize", "_DepthSize", "DepthSize" );
		private static readonly PropertyNames m_impostorSizeNames = new PropertyNames( "_AI_ImpostorSize", "_ImpostorSize", "Impostor Size" );
		private static readonly PropertyNames m_offsetNames = new PropertyNames( "_AI_Offset", "_Offset", "Offset" );
		private static readonly PropertyNames m_sizeOffsetNames = new PropertyNames( "_AI_SizeOffset", null, "Size & Offset" );
		private static readonly PropertyNames m_boundsMinNames = new PropertyNames( "_AI_BoundsMin", null, "Bounds Min" );
		private static readonly PropertyNames m_boundsSizeNames = new PropertyNames( "_AI_BoundsSize", null, "Bounds Size" );

		private bool m_propertiesInitialized = false;

		//private static readonly string ASEUpgradeMessage1 = "To work correctly, this version of Amplify Impostor Node requires Amplify Shader Editor v1.9.8.2 or higher. Please update ASE to latest.";
		private static readonly string NodeErrorMsg = "Amplify Impostor node does not support Surface shaders. To create Amplify Impostor runtime shaders for Built-in RP, please use a template shader; e.g. Legacy/Lit.";
		private static readonly string ErrorOnCompilationMsg = "Attempting to use Amplify Impostor node on Surface shaders. Only template-based shaders are supported.";

		protected override void CommonInit( int uniqueId )
		{
			base.CommonInit( uniqueId );

			//if ( AmplifyShaderEditor.VersionInfo.FullNumber < 19802 )
			//{
			//	Debug.LogWarning( "[AmplifyImpostors] " + ASEUpgradeMessage1 );
			//}

			for ( int i = 0; i < MaxExtraSamplers; i++ )
			{
				AddInputPort( WirePortDataType.SAMPLER2D, true, "Tex" + i );
				AddOutputPort( WirePortDataType.FLOAT4, "Tex" + i, i + 8 );
			}

			AddInputPort( WirePortDataType.SAMPLERSTATE, false, "SS" );
			m_samplerStatePort = m_inputPorts[ m_inputPorts.Count - 1 ];

			AddOutputPort( WirePortDataType.FLOAT3, "Albedo", 0 );
			AddOutputPort( WirePortDataType.FLOAT3, "World Normal", 1 );
			AddOutputPort( WirePortDataType.FLOAT3, "Specular", 3 );
			AddOutputPort( WirePortDataType.FLOAT, "Smoothness", 4 );
			AddOutputPort( WirePortDataType.FLOAT, "Occlusion", 5 );
			AddOutputPort( WirePortDataType.FLOAT3, "Emission", 2 );
			AddOutputPort( WirePortDataType.FLOAT, "Alpha", 6 );
			AddOutputPort( WirePortDataType.FLOAT, "Depth", 17 );
			AddOutputPort( WirePortDataType.FLOAT3, "World Position", 7 );
			AddOutputPort( WirePortDataType.FLOAT3, "View Position", 16 );

			m_autoWrapProperties = true;
			m_textLabelWidth = 160;

			m_errorMessageTooltip = NodeErrorMsg;
			m_errorMessageTypeIsError = NodeMessageType.Error;

			UpdateTitle();
			UpdatePorts();
			UpdateInputPorts();
		}

		public override void AfterCommonInit()
		{
			base.AfterCommonInit();
			UpdateTag();
		}

		void UpdateTag()
		{
			if ( m_containerGraph.CurrentMasterNode != null && !m_showErrorMessage )
			{
				List<CustomTagData> allTags = null;
				if ( VersionInfo.FullNumber > 15500 )
				{
					allTags = ( ( TemplateMultiPassMasterNode )m_containerGraph.CurrentMasterNode ).SubShaderModule.TagsHelper.AvailableTags;
				}
				else
				{
					allTags = ( m_containerGraph.MultiPassMasterNodes.NodesList[ m_containerGraph.MultiPassMasterNodes.Count - 1 ] ).SubShaderModule.TagsHelper.AvailableTags;
				}

				CustomTagData importorTag = allTags.Find( x => x.TagName == "ImpostorType" );
				if ( importorTag != null )
					importorTag.TagValue = m_customImpostorType.ToString();
				else
					allTags.Add( new CustomTagData( "ImpostorType", m_customImpostorType.ToString(), 0 ) );
			}
		}

		public override void DrawProperties()
		{
			//if ( AmplifyShaderEditor.VersionInfo.FullNumber < 19802 )
			//{
			//	EditorGUILayout.HelpBox( ASEUpgradeMessage1, MessageType.Warning );
			//}

			base.DrawProperties();
			EditorGUI.BeginChangeCheck();
			m_customImpostorType = ( CustomImpostorType )EditorGUILayoutEnumPopup( "Impostor Type", m_customImpostorType );
			if ( EditorGUI.EndChangeCheck() )
			{
				UpdateTitle();
				UpdateTag();
			}

			EditorGUI.BeginChangeCheck();
			m_workflow = ( ASEStandardSurfaceWorkflow )EditorGUILayoutEnumPopup( WorkflowStr, m_workflow );
			if ( EditorGUI.EndChangeCheck() )
			{
				UpdatePorts();
			}

			if ( IsSpherical )
			{
				m_parallaxSupport = EditorGUILayoutToggle( "Parallax Support", m_parallaxSupport );
			}
			else // if any type of octahedron
			{
				if ( !IsCombinedOctahedron )
				{
					m_hemiToggleSupport = EditorGUILayoutToggle( "Hemi Toggle Support", m_hemiToggleSupport );
				}
				m_frameClampSupport = EditorGUILayoutToggle( "Frame Clamp Support", m_frameClampSupport );
			}

			m_speedTreeHueSupport = EditorGUILayoutToggle( "SpeedTree Hue Support", m_speedTreeHueSupport );

			m_positionTextureSupport =  EditorGUILayoutToggle( "Position Texture Support", m_positionTextureSupport );

			EditorGUI.BeginChangeCheck();
			m_matchPropertyNames = EditorGUILayoutToggle( "Match Native Property Names", m_matchPropertyNames );
			if ( EditorGUI.EndChangeCheck() )
			{
				UpdatePropertyNames();
			}

			EditorGUI.BeginChangeCheck();
			m_showExtraData = EditorGUILayoutToggle( "Output Extra Data", m_showExtraData );
			if ( EditorGUI.EndChangeCheck() )
			{
				UpdatePorts();
			}

			EditorGUI.BeginChangeCheck();
			float cacha = EditorGUIUtility.labelWidth;
			EditorGUIUtility.labelWidth = 120;
			m_extraSamplers = EditorGUILayoutIntSlider( "Extra Samplers", m_extraSamplers, 0, MaxExtraSamplers );
			if ( EditorGUI.EndChangeCheck() )
			{
				UpdateInputPorts();
			}
			EditorGUIUtility.labelWidth = cacha;

			if ( m_showErrorMessage )
			{
				EditorGUILayout.HelpBox( NodeErrorMsg, MessageType.Error );
			}
		}

		public void UpdateInputPorts()
		{
			m_extraPropertyNames = new string[ m_extraSamplers ];
			for ( int i = 0; i < MaxExtraSamplers; i++ )
			{
				GetOutputPortByArrayId( i ).Visible = i < m_extraSamplers;
				GetInputPortByArrayId( i ).Visible = i < m_extraSamplers;
			}
			m_samplerStatePort.Visible = m_extraSamplers > 0;
			m_sizeIsDirty = true;
		}

		public void UpdatePorts()
		{
			if ( m_showExtraData )
			{
				GetOutputPortByUniqueId( 7 ).Visible = true;
				GetOutputPortByUniqueId( 16 ).Visible = true;
			}
			else
			{
				GetOutputPortByUniqueId( 7 ).Visible = false;
				GetOutputPortByUniqueId( 16 ).Visible = false;
			}

			if ( m_workflow == ASEStandardSurfaceWorkflow.Specular )
			{
				GetOutputPortByUniqueId( 3 ).ChangeProperties( "Specular", WirePortDataType.FLOAT3, false );
			}
			else
			{
				GetOutputPortByUniqueId( 3 ).ChangeProperties( "Metallic", WirePortDataType.FLOAT, false );
			}
			m_sizeIsDirty = true;
		}

		void UpdatePropertyNames()
		{
			foreach ( var pair in m_propertyNodes )
			{
				PropertyEntry entry = pair.Value;
				if ( !string.IsNullOrEmpty( entry.names.legacy ) )
				{
					entry.prop.RegisterPropertyName( true, m_matchPropertyNames ? entry.names.legacy : entry.names.unique );
				}
				ASEHelper.PropertyNode.SetInspectorName( entry.prop, entry.names.inspector );
			}
		}

		void InitProperty<T>( ref T prop, int order, PropertyNames names, bool hideInInspector = false,
			bool toggle = false ) where T : PropertyNode
		{
			prop = ( prop != null ) ? prop : ScriptableObject.CreateInstance<T>();
			prop.ContainerGraph = ContainerGraph;
			prop.OrderIndex = order;
			prop.UniqueId = ContainerGraph.GetValidId();
			prop.ChangeParameterType( PropertyType.Property );
			prop.RegisterPropertyName( true, names.unique );

			UIUtils.RegisterPropertyNode( prop );
			ASEHelper.PropertyNode.SetInspectorName( prop, names.inspector );

			if ( hideInInspector )
			{
				ASEHelper.PropertyNode.AddAttribute( prop, 0 );
			}

			if ( toggle )
			{
				ASEHelper.PropertyNode.AddAttribute( prop, 5 );
			}

			m_propertyNodes.TryAdd( names.unique, new PropertyEntry( prop, names ) );
		}

		void InitFloatProperty( ref RangedFloatNode prop, int order, PropertyNames names, bool hideInInspector = false, bool toggle = false,
			float value = 0, bool floatMode = true, float min = 0, float max = 0 )
		{
			InitProperty<RangedFloatNode>( ref prop, order, names, hideInInspector, toggle );

			UIUtils.RegisterFloatIntNode( prop );
			prop.Value = value;
			prop.SetFloatMode( floatMode );

			if ( !floatMode )
			{
				ASEHelper.RangedFloatNode.SetMinMax( prop, min, max );
			}
		}

		void InitTextureProperty( ref TexturePropertyNode prop, int order, PropertyNames names, TexturePropertyValues value )
		{
			InitProperty<TexturePropertyNode>( ref prop, order, names );

			UIUtils.RegisterTexturePropertyNode( prop );
			prop.DefaultTextureValue = value;
			prop.CustomPrefix = names.unique.Substring( 1 );
			prop.DrawAutocast = false;

			ASEHelper.PropertyNode.AddAttribute( prop, 5 );
		}

		public void Init()
		{
			if ( !m_propertiesInitialized )
			{
				InitTextureProperty( ref m_albedoTexture, m_orderAlbedoTexture, m_albedoTextureNames, TexturePropertyValues.white );
				InitTextureProperty( ref m_normalTexture, m_orderNormalTexture, m_normalTextureNames, TexturePropertyValues.white );
				InitTextureProperty( ref m_specularTexture, m_orderSpecularTexture, m_specularTextureNames, TexturePropertyValues.black );
				InitTextureProperty( ref m_occlusionTexture, m_orderOcclusionTexture, m_occlusionTextureNames, TexturePropertyValues.white );
				InitTextureProperty( ref m_emissionTexture, m_orderEmissionTexture, m_emissionTextureNames, TexturePropertyValues.black );
				InitTextureProperty( ref m_positionTexture, m_orderPositionTexture, m_positionTextureNames, TexturePropertyValues.black );
				InitFloatProperty( ref m_clipProp, m_orderClipProp, m_clipNames, value: 0.5f, floatMode: false, min: 0, max: 1 );
				InitFloatProperty( ref m_textureBiasProp, m_orderTextureBiasProp, m_textureBiasNames, value: -1 );
				InitFloatProperty( ref m_useParallaxProp, m_orderUseParallaxProp, m_useParallaxNames, value: 0, toggle: true );
				InitFloatProperty( ref m_parallaxProp, m_orderParallaxProp, m_parallaxNames, value: 1, floatMode: false, min: -1, max: 1 );
				InitFloatProperty( ref m_shadowBiasProp, m_orderShadowBiasProp, m_shadowBiasNames, value: 0.333f, floatMode: false, min: 0, max: 2 );
				InitFloatProperty( ref m_shadowViewProp, m_orderShadowViewProp, m_shadowViewNames, value: 1, floatMode: false, min: 0, max: 1 );
				InitFloatProperty( ref m_forwardBiasProp, m_orderForwardBiasProp, m_forwardBiasNames, value: 0.0f, floatMode: false, min: 0, max: 2 );
				InitFloatProperty( ref m_hemiProp, m_orderHemiProp, m_hemiNames, value: 0, toggle: true );
				InitFloatProperty( ref m_useHueProp, m_orderUseHueProp, m_useHueNames, value: 0, toggle: true );
				InitProperty<ColorNode>( ref m_hueProp, m_orderHueProp, m_hueNames );
				InitFloatProperty( ref m_clipNeighborsProp, m_orderClipNeighbors, m_clipNeighborsNames, value: 0, toggle: true );
				InitFloatProperty( ref m_framesProp, m_orderFramesProp, m_framesNames, hideInInspector: true, value: 16 );
				InitFloatProperty( ref m_framesXProp, m_orderFramesXProp, m_framesXNames, hideInInspector: true, value: 16 );
				InitFloatProperty( ref m_framesYProp, m_orderFramesYProp, m_framesYNames, hideInInspector: true, value: 16 );
				InitFloatProperty( ref m_depthProp, m_orderDepthProp, m_depthNames, hideInInspector: true, value: 1 );
				InitFloatProperty( ref m_impostorSizeProp, m_orderImpostorSizeProp, m_impostorSizeNames, hideInInspector: true, value: 1 );
				InitProperty<Vector3Node>( ref m_offsetProp, m_orderOffsetProp, m_offsetNames, hideInInspector: true );
				InitProperty<Vector4Node>( ref m_sizeOffsetProp, m_orderSizeOffsetProp, m_sizeOffsetNames, hideInInspector: true );
				InitProperty<Vector3Node>( ref m_boundsMinProp, m_orderBoundsMinProp, m_boundsMinNames, hideInInspector: true );
				InitProperty<Vector3Node>( ref m_boundsSizeProp, m_orderBoundsSizeProp, m_boundsSizeNames, hideInInspector: true );

				UpdatePropertyNames();

				m_propertiesInitialized = true;
			}
		}

		public override void SetMaterialMode( Material mat, bool fetchMaterialValues )
		{
			base.SetMaterialMode( mat, fetchMaterialValues );

			if ( !m_propertiesInitialized )
				return;

			foreach ( var pair in m_propertyNodes )
			{
				pair.Value.prop.SetMaterialMode( mat, fetchMaterialValues );
			}
		}

		public override void RefreshExternalReferences()
		{
			base.RefreshExternalReferences();

			Init();

			UpdateTitle();
			UpdatePorts();
			UpdateInputPorts();
		}

		void UpdateTitle()
		{
			SetAdditonalTitleText( "Type( " + m_customImpostorType + " )" );

			//List<CustomTagData> allTags = null;
			//if( VersionInfo.FullNumber > 15500 )
			//{
			//	allTags = ( (TemplateMultiPassMasterNode)m_containerGraph.CurrentMasterNode ).SubShaderModule.TagsHelper.AvailableTags;
			//}
			//else
			//{
			//	allTags = ( m_containerGraph.MultiPassMasterNodes.NodesList[ m_containerGraph.MultiPassMasterNodes.Count - 1 ] ).SubShaderModule.TagsHelper.AvailableTags;
			//}

			//CustomTagData importorTag = allTags.Find( x => x.TagName == "ImpostorType" );
			//if( importorTag != null )
			//	importorTag.TagValue = m_customImpostorType.ToString();
			//else
			//	allTags.Add( new CustomTagData( "ImpostorType", m_customImpostorType.ToString(), 0 ) );
		}

		public override void OnNodeLogicUpdate( DrawInfo drawInfo )
		{
			base.OnNodeLogicUpdate( drawInfo );

			Init();

			foreach ( var pair in m_propertyNodes )
			{
				pair.Value.prop.OnNodeLogicUpdate( drawInfo );
			}

			m_showErrorMessage = m_containerGraph != null && m_containerGraph.ParentWindow != null &&
				m_containerGraph.ParentWindow.MainGraphInstance.CurrentCanvasMode != NodeAvailability.TemplateShader &&
				m_containerGraph.ParentWindow.MainGraphInstance.CurrentCanvasMode != NodeAvailability.ShaderFunction;
		}

		public override void Destroy()
		{
			base.Destroy();

			foreach ( var pair in m_propertyNodes )
			{
				pair.Value.prop.Destroy();
			}
		}

		public override string GenerateShaderForOutput( int outputId, ref MasterNodeDataCollector dataCollector, bool ignoreLocalvar )
		{
			if ( m_showErrorMessage )
			{
				UIUtils.ShowMessage( ErrorOnCompilationMsg, MessageSeverity.Error );
				return GenerateErrorValue();
			}

			UpdateInputPorts();

			bool hasSpec = true;
			if ( !GetOutputPortByUniqueId( 3 ).IsConnected && !GetOutputPortByUniqueId( 4 ).IsConnected )
				hasSpec = false;

			bool hasOcclusion = true;
			if ( !GetOutputPortByUniqueId( 5 ).IsConnected )
				hasOcclusion = false;

			bool hasEmission = true;
			if ( !GetOutputPortByUniqueId( 2 ).IsConnected )
				hasEmission = false;

			bool hasPosition = m_positionTextureSupport;
			bool isHDRP = ( dataCollector.IsSRP && dataCollector.CurrentSRPType == TemplateSRPType.HDRP );

			bool generateSamplingMacros = UIUtils.CurrentWindow.OutsideGraph.CurrentMasterNode.SamplingMacros;
			m_albedoTexture.ForceSamplingMacrosGen = generateSamplingMacros;
			m_normalTexture.ForceSamplingMacrosGen = generateSamplingMacros;
			m_specularTexture.ForceSamplingMacrosGen = generateSamplingMacros;
			m_occlusionTexture.ForceSamplingMacrosGen = generateSamplingMacros;
			m_emissionTexture.ForceSamplingMacrosGen = generateSamplingMacros;
			m_positionTexture.ForceSamplingMacrosGen = generateSamplingMacros;

			m_albedoTexture.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			m_normalTexture.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			if ( hasSpec )
			{
				m_specularTexture.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
				dataCollector.AddToPragmas( UniqueId, "shader_feature_local_fragment _SPECULARMAP" );
			}

			if ( hasOcclusion )
			{
				m_occlusionTexture.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
				dataCollector.AddToPragmas( UniqueId, "shader_feature_local_fragment _OCCLUSIONMAP" );
			}

			if ( hasEmission )
			{
				m_emissionTexture.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
				dataCollector.AddToPragmas( UniqueId, "shader_feature_local_fragment _EMISSIONMAP" );
			}

			if ( m_positionTextureSupport )
			{
				m_positionTexture.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
				dataCollector.AddToPragmas( UniqueId, "shader_feature_local_fragment _POSITIONMAP" );
			}

			m_clipProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			m_textureBiasProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );

			if ( IsAnyOctahedron || ( IsSpherical && m_parallaxSupport ) )
			{
				if ( IsSpherical )
				{
					dataCollector.AddToProperties( UniqueId, string.Format( "[Toggle( _USE_PARALLAX_ON )] {0}( \"{1}\", Float ) = {2}", m_useParallaxProp.PropertyName, m_useParallaxProp.PropertyInspectorName, m_useParallaxProp.Value ), m_useParallaxProp.OrderIndex );
				}
				m_parallaxProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
				dataCollector.AddToPragmas( UniqueId, "shader_feature_local _USE_PARALLAX_ON" );
			}

			m_shadowBiasProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			m_shadowViewProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			m_forwardBiasProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );

			if ( m_speedTreeHueSupport )
			{
				dataCollector.AddToProperties( UniqueId, string.Format( "[Toggle( EFFECT_HUE_VARIATION )] {0}( \"{1}\", Float ) = {2}", m_useHueProp.PropertyName, m_useHueProp.PropertyInspectorName, m_useHueProp.Value ), m_useHueProp.OrderIndex );
				m_hueProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
				dataCollector.AddToPragmas( UniqueId, "shader_feature_local EFFECT_HUE_VARIATION" );
			}

			if ( IsAnyOctahedron && m_hemiToggleSupport || IsCombinedOctahedron )
			{
				dataCollector.AddToProperties( UniqueId, string.Format( "[Toggle( _HEMI_ON )] {0}( \"{1}\", Float ) = {2}", m_hemiProp.PropertyName, m_hemiProp.PropertyInspectorName, m_hemiProp.Value ), m_hemiProp.OrderIndex );
				dataCollector.AddToPragmas( UniqueId, "shader_feature_local _HEMI_ON" );
			}

			if ( IsAnyOctahedron && m_frameClampSupport )
			{
				dataCollector.AddToProperties( UniqueId, string.Format( "[Toggle( AI_CLIP_NEIGHBOURS_FRAMES )] AI_CLIP_NEIGHBOURS_FRAMES( \"{0}\", Float ) = {1}", m_clipNeighborsProp.PropertyInspectorName, m_clipNeighborsProp.Value ), m_clipNeighborsProp.OrderIndex );
				dataCollector.AddToPragmas( UniqueId, "shader_feature_local AI_CLIP_NEIGHBOURS_FRAMES" );
			}

			if ( IsAnyOctahedron )
			{
				m_framesProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			}
			else if ( IsSpherical )
			{
				m_framesXProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
				m_framesYProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			}
			m_depthProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			m_impostorSizeProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			m_offsetProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			m_sizeOffsetProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			m_boundsMinProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );
			m_boundsSizeProp.GenerateShaderForOutput( 0, ref dataCollector, ignoreLocalvar );

			dataCollector.AddFunction( ImpostorOutputStructDeclaration[ 0 ], ImpostorOutputStructDeclaration, true );

			if ( dataCollector.IsSRP )
			{
				dataCollector.AddToDefines( UniqueId, "AI_ObjectToWorld GetObjectToWorldMatrix()" );
				dataCollector.AddToDefines( UniqueId, "AI_WorldToObject GetWorldToObjectMatrix()" );
				dataCollector.AddToDefines( UniqueId, "AI_INV_TWO_PI  INV_TWO_PI" );
				dataCollector.AddToDefines( UniqueId, "AI_PI          PI" );
				dataCollector.AddToDefines( UniqueId, "AI_INV_PI      INV_PI" );
			}
			else
			{
				dataCollector.AddToDefines( UniqueId, "AI_ObjectToWorld unity_ObjectToWorld" );
				dataCollector.AddToDefines( UniqueId, "AI_WorldToObject unity_WorldToObject" );
				dataCollector.AddToDefines( UniqueId, "AI_INV_TWO_PI  UNITY_INV_TWO_PI" );
				dataCollector.AddToDefines( UniqueId, "AI_PI          UNITY_PI" );
				dataCollector.AddToDefines( UniqueId, "AI_INV_PI      UNITY_INV_PI" );
			}

			if ( AmplifyShaderEditor.VersionInfo.FullNumber < 19802 )
			{
				// @diogo: These are automatically added, when needed, from ASE 1.9.8.2 or higher
				dataCollector.AddToDefines( UniqueId, "ASE_NEEDS_TEXTURE_COORDINATES1" );
				dataCollector.AddToDefines( UniqueId, "ASE_NEEDS_TEXTURE_COORDINATES2" );
			}

			dataCollector.AddToDefines( UniqueId, "ASE_CHANGES_WORLD_POS" );

			if ( IsAnyOctahedron )
			{
				if ( m_hemiToggleSupport || IsOctahedron || IsCombinedOctahedron )
				{
					GenerateVectorToOctahedron();
					dataCollector.AddFunctions( "VectorToOctahedron", m_functionBody );

					GenerateOctahedronToVector();
					dataCollector.AddFunctions( "OctahedronToVector", m_functionBody );

					GenerateOctahedronRayPlaneIntersectionUV();
					dataCollector.AddFunctions( "RayPlaneIntersectionUV", m_functionBody );
				}

				if ( m_hemiToggleSupport || IsHemiOctahedron || IsCombinedOctahedron )
				{
					GenerateVectorToHemiOctahedron();
					dataCollector.AddFunctions( "VectorToHemiOctahedron", m_functionBody );

					GenerateHemiOctahedronToVector();
					dataCollector.AddFunctions( "HemiOctahedronToVector", m_functionBody );

					GenerateOctahedronRayPlaneIntersectionUV();
					dataCollector.AddFunctions( "RayPlaneIntersectionUV", m_functionBody );
				}
			}

			GenerateImpostorClipPosDepthBias();
			dataCollector.AddFunctions( "ImpostorClipPosDepthBias", m_functionBody );

			if ( IsSpherical )
				GenerateSphereImpostorVertex( ref dataCollector );
			else
				GenerateImpostorVertex( ref dataCollector );

			string uvFrame1Name = "UVsFrame1" + OutputId;
			string uvFrame2Name = "UVsFrame2" + OutputId;
			string uvFrame3Name = "UVsFrame3" + OutputId;
			string octaFrameName = "octaframe" + OutputId;
			string sphereFrames = "frameUVs" + OutputId;
			string viewPosName = "viewPos" + OutputId;
			TemplateVertexData data = null;

			if ( IsSpherical )
			{
				data = dataCollector.TemplateDataCollectorInstance.RequestNewInterpolator( WirePortDataType.FLOAT4, false, sphereFrames );
				if ( data != null )
					sphereFrames = data.VarName;
			}
			else
			{
				data = dataCollector.TemplateDataCollectorInstance.RequestNewInterpolator( WirePortDataType.FLOAT4, false, uvFrame1Name );
				if ( data != null )
					uvFrame1Name = data.VarName;
				data = dataCollector.TemplateDataCollectorInstance.RequestNewInterpolator( WirePortDataType.FLOAT4, false, uvFrame2Name );
				if ( data != null )
					uvFrame2Name = data.VarName;
				data = dataCollector.TemplateDataCollectorInstance.RequestNewInterpolator( WirePortDataType.FLOAT4, false, uvFrame3Name );
				if ( data != null )
					uvFrame3Name = data.VarName;
				data = dataCollector.TemplateDataCollectorInstance.RequestNewInterpolator( WirePortDataType.FLOAT4, false, octaFrameName );
				if ( data != null )
					octaFrameName = data.VarName;
			}
			data = dataCollector.TemplateDataCollectorInstance.RequestNewInterpolator( WirePortDataType.FLOAT4, false, viewPosName );
			if ( data != null )
				viewPosName = data.VarName;

			MasterNodePortCategory portCategory = dataCollector.PortCategory;
			dataCollector.PortCategory = MasterNodePortCategory.Vertex;
			//Debug.Log( dataCollector.TemplateDataCollectorInstance.CurrentTemplateData.FragmentFunctionData.InVarType );
			string vertOut = dataCollector.TemplateDataCollectorInstance.CurrentTemplateData.VertexFunctionData.OutVarName;
			string vertIN = dataCollector.TemplateDataCollectorInstance.CurrentTemplateData.VertexFunctionData.InVarName;

			dataCollector.AddToDirectives( "#undef ASE_ADJUST_CLIP_POSITION" );
			dataCollector.AddToDefines( UniqueId, string.Format( "ASE_ADJUST_CLIP_POSITION( x ) ImpostorClipPosDepthBias( x, {0}, {1} )",
				vertOut + "." + viewPosName, m_depthProp.PropertyName ) );

			string positionOS = dataCollector.IsSRP ? ".positionOS.xyz" : ".vertex.xyz";
			string normalOS = dataCollector.IsSRP ? ".normalOS.xyz" : ".normal.xyz";
			string tangentOS = dataCollector.IsSRP ? ".tangentOS" : ".tangent";

			string functionResult;
			if ( IsSpherical )
			{
				functionResult = dataCollector.AddFunctions( m_functionHeaderSphere, m_functionBody, vertIN + positionOS, vertIN + normalOS, vertIN + tangentOS, vertOut + "." + sphereFrames, vertOut + "." + viewPosName );
			}
			else
			{
				functionResult = dataCollector.AddFunctions( m_functionHeader, m_functionBody, vertIN + positionOS, vertIN + normalOS, vertIN + tangentOS, vertOut + "." + uvFrame1Name, vertOut + "." + uvFrame2Name, vertOut + "." + uvFrame3Name, vertOut + "." + octaFrameName, vertOut + "." + viewPosName );
			}

			dataCollector.AddLocalVariable( UniqueId, functionResult + ";" );

			dataCollector.PortCategory = portCategory;

			if ( dataCollector.IsFragmentCategory )
			{
				string extraHeader = string.Empty;

				for ( int i = 0; i < m_extraSamplers; i++ )
				{
					if ( GetInputPortByArrayId( i ).IsConnected && GetOutputPortByArrayId( i ).IsConnected )
					{
						m_extraPropertyNames[ i ] = GetInputPortByArrayId( i ).GeneratePortInstructions( ref dataCollector );
						dataCollector.AddLocalVariable( UniqueId, "float4 output" + i + " = 0;" );
						extraHeader += ", output" + i;
					}
				}

				string positionCS = dataCollector.TemplateDataCollectorInstance.GetClipPos();
				string positionWS = dataCollector.TemplateDataCollectorInstance.GetPosition( isHDRP ? PositionNode.Space.RelativeWorld : PositionNode.Space.World );
				dataCollector.AddLocalVariable( UniqueId, "ImpostorOutput io = ( ImpostorOutput )0;" );

				string finalHeader = m_functionHeaderFrag;
				if ( IsSpherical )
				{
					finalHeader = m_functionHeaderSphereFrag;
					finalHeader = finalHeader.Replace( "{3}", "{3}" + extraHeader );
				}
				else
				{
					finalHeader = finalHeader.Replace( "{6}", "{6}" + extraHeader );

				}

				string fragIN = dataCollector.TemplateDataCollectorInstance.CurrentTemplateData.FragmentFunctionData.InVarName;
				if ( IsSpherical )
				{
					GenerateSphereImpostorFragment( ref dataCollector, hasSpec, hasOcclusion, hasEmission, hasPosition );
					functionResult = dataCollector.AddFunctions( finalHeader, m_functionBody, positionCS, positionWS, fragIN + "." + sphereFrames, fragIN + "." + viewPosName );
				}
				else
				{
					GenerateImpostorFragment( ref dataCollector, hasSpec, hasOcclusion, hasEmission, hasPosition );
					functionResult = dataCollector.AddFunctions( finalHeader, m_functionBody, positionCS, positionWS, fragIN + "." + uvFrame1Name, fragIN + "." + uvFrame2Name, fragIN + "." + uvFrame3Name, fragIN + "." + octaFrameName, fragIN + "." + viewPosName );
				}
				dataCollector.AddLocalVariable( UniqueId, functionResult + ";" );

				switch ( outputId )
				{
					case 0:
						return "io.Albedo";
					case 1:
						return "io.WorldNormal";
					case 2:
						return "io.Emission";
					case 3:
						return m_workflow == ASEStandardSurfaceWorkflow.Specular ? "io.Specular" : "io.Metallic";
					case 4:
						return "io.Smoothness";
					case 5:
						return "io.Occlusion";
					case 6:
						return "io.Alpha";
					case 7:
						return isHDRP ? string.Format( "GetAbsolutePositionWS( {0} )", positionWS ): positionWS;
					case 17:
						return string.Format( "( {0}.z / {0}.w )", positionCS );
					case 16:
						dataCollector.AddLocalVariable( UniqueId, string.Format( "float3 viewPosOut{0} = mul( UNITY_MATRIX_V, float4( {1}.xyz, 1.0 ) ).xyz;", OutputId, positionWS ) );
						return "viewPosOut" + OutputId;
					default:
						return "output" + ( outputId - 8 );
				}
			}
			else
			{
				switch ( outputId )
				{
					case 7:
					{
						string positionWS;
						if ( !dataCollector.IsSRP )
						{
							positionWS = string.Format( "mul( unity_ObjectToWorld, float4( {0}{1}.xyz, 1.0 ) ).xyz", vertIN, positionOS );
						}
						else
						{
							positionWS = string.Format( "TransformObjectToWorld( {0}{1} )", vertIN, positionOS );
							positionWS = isHDRP ? "GetAbsolutePositionWS( " + positionWS + " )" : positionWS;
						}
						dataCollector.AddLocalVariable( UniqueId, "float3 worldPosOut" + OutputId + " = " + positionWS + ";" );
						return "worldPosOut" + OutputId;
					}
					case 16:
						return vertOut + "." + viewPosName + ".xyz";
					default:
						return "0";
				}
			}
		}

		public override void WriteToString( ref string nodeInfo, ref string connectionsInfo )
		{
			base.WriteToString( ref nodeInfo, ref connectionsInfo );

			IOUtils.AddFieldValueToString( ref nodeInfo, AmplifyImpostors.VersionInfo.FullNumber );

			IOUtils.AddFieldValueToString( ref nodeInfo, m_customImpostorType );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_hemiToggleSupport );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_parallaxSupport );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_speedTreeHueSupport );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_showExtraData );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_frameClampSupport );

			IOUtils.AddFieldValueToString( ref nodeInfo, m_framesProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_framesXProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_framesYProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_impostorSizeProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_parallaxProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_offsetProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_sizeOffsetProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_textureBiasProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_albedoTexture.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_normalTexture.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_specularTexture.OrderIndex.ToString() );

			IOUtils.AddFieldValueToString( ref nodeInfo, m_emissionTexture.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_depthProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_shadowBiasProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_shadowViewProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_clipProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_hueProp.OrderIndex.ToString() );

			IOUtils.AddFieldValueToString( ref nodeInfo, m_occlusionTexture.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_positionTexture.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_useParallaxProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_hemiProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_useHueProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_clipNeighborsProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_forwardBiasProp.OrderIndex.ToString() );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_positionTextureSupport );

			IOUtils.AddFieldValueToString( ref nodeInfo, m_extraSamplers );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_workflow );
			IOUtils.AddFieldValueToString( ref nodeInfo, m_matchPropertyNames );
		}

		public override void ReadFromString( ref string[] nodeParams )
		{
			base.ReadFromString( ref nodeParams );

			int version = 0;
			if ( UIUtils.CurrentShaderVersion() > 15602 && AmplifyImpostors.VersionInfo.FullNumber >= 9202 )
				version = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );

			m_customImpostorType = ( CustomImpostorType )Enum.Parse( typeof( CustomImpostorType ), GetCurrentParam( ref nodeParams ) );
			if ( version >= 9906 )
			{
				m_hemiToggleSupport = Convert.ToBoolean( GetCurrentParam( ref nodeParams ) );
				m_parallaxSupport = Convert.ToBoolean( GetCurrentParam( ref nodeParams ) );
			}
			m_speedTreeHueSupport = Convert.ToBoolean( GetCurrentParam( ref nodeParams ) );
			m_showExtraData = Convert.ToBoolean( GetCurrentParam( ref nodeParams ) );
			if ( version >= 9202 )
			{
				m_frameClampSupport = Convert.ToBoolean( GetCurrentParam( ref nodeParams ) );
			}
			m_orderFramesProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			m_orderFramesXProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			m_orderFramesYProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			m_orderImpostorSizeProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			m_orderParallaxProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			m_orderOffsetProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			if ( version >= 9500 )
			{
				m_orderSizeOffsetProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			}
			m_orderTextureBiasProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			m_orderAlbedoTexture = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			m_orderNormalTexture = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			m_orderSpecularTexture = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			m_orderEmissionTexture = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			m_orderDepthProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			m_orderShadowBiasProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			if ( version >= 9300 )
			{
				m_orderShadowViewProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			}
			m_orderClipProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
			m_orderHueProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );

			if ( version >= 9906 )
			{
				m_orderOcclusionTexture = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
				m_orderPositionTexture = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
				m_orderUseParallaxProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
				m_orderHemiProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
				m_orderUseHueProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
				m_orderClipNeighbors = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
				m_orderForwardBiasProp = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
				m_positionTextureSupport = Convert.ToBoolean( GetCurrentParam( ref nodeParams ) );
			}

			if ( UIUtils.CurrentShaderVersion() > 15405 )
			{
				m_extraSamplers = Convert.ToInt32( GetCurrentParam( ref nodeParams ) );
				m_workflow = ( ASEStandardSurfaceWorkflow )Enum.Parse( typeof( ASEStandardSurfaceWorkflow ), GetCurrentParam( ref nodeParams ) );
			}
			if ( version > 9707 )
			{
				m_matchPropertyNames = Convert.ToBoolean( GetCurrentParam( ref nodeParams ) );
			}

			UpdateTitle();
			UpdatePorts();
			UpdateInputPorts();
		}

		private void GenerateVectorToOctahedron()
		{
			m_functionBody = string.Empty;
			IOUtils.AddFunctionHeader( ref m_functionBody, "float2 VectorToOctahedron( float3 N )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "N /= dot( 1.0, abs( N ) );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "if( N.z <= 0 )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "N.xy = ( 1 - abs( N.yx ) ) * ( N.xy >= 0 ? 1.0 : -1.0 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "return N.xy;" );
			IOUtils.CloseFunctionBody( ref m_functionBody );
		}

		private void GenerateOctahedronToVector()
		{
			m_functionBody = string.Empty;
			IOUtils.AddFunctionHeader( ref m_functionBody, "float3 OctahedronToVector( float2 Oct )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 N = float3( Oct, 1.0 - dot( 1.0, abs( Oct ) ) );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "if(N.z< 0 )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "N.xy = ( 1 - abs( N.yx) ) * (N.xy >= 0 ? 1.0 : -1.0 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "return normalize( N);" );
			IOUtils.CloseFunctionBody( ref m_functionBody );
		}

		private void GenerateVectorToHemiOctahedron()
		{
			m_functionBody = string.Empty;
			IOUtils.AddFunctionHeader( ref m_functionBody, "float2 VectorToHemiOctahedron( float3 N )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "N.xy /= dot( 1.0, abs( N ) );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "return float2( N.x + N.y, N.x - N.y );" );
			IOUtils.CloseFunctionBody( ref m_functionBody );
		}

		private void GenerateHemiOctahedronToVector()
		{
			m_functionBody = string.Empty;
			IOUtils.AddFunctionHeader( ref m_functionBody, "float3 HemiOctahedronToVector( float2 Oct )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "Oct = float2( Oct.x + Oct.y, Oct.x - Oct.y ) * 0.5;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 N = float3( Oct, 1 - dot( 1.0, abs( Oct ) ) );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "return normalize( N );" );
			IOUtils.CloseFunctionBody( ref m_functionBody );
		}

		private void GenerateOctahedronRayPlaneIntersectionUV()
		{
			m_functionBody = string.Empty;
			IOUtils.AddFunctionHeader( ref m_functionBody, "inline void RayPlaneIntersectionUV( float3 normalOS, float3 rayPosition, float3 rayDirection, out float2 uvs, out float3 localNormal )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float lDotN = dot( rayDirection, normalOS ); " );
			IOUtils.AddFunctionLine( ref m_functionBody, "float p0l0DotN = dot( -rayPosition, normalOS );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float t = p0l0DotN / lDotN;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 p = rayDirection * t + rayPosition;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 upVector = float3( 0, 1, 0 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 tangent = normalize( cross( upVector, normalOS ) + float3( -0.001, 0, 0 ) );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 bitangent = cross( tangent, normalOS );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float frameX = dot( p, tangent );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float frameZ = dot( p, bitangent );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "uvs = -float2( frameX, frameZ );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "if( t <= 0.0 )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "uvs = 0;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3x3 worldToLocal = float3x3( tangent, bitangent, normalOS );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "localNormal = normalize( mul( worldToLocal, rayDirection ) );" );
			IOUtils.CloseFunctionBody( ref m_functionBody );
		}

		private void GenerateImpostorClipPosDepthBias()
		{
			m_functionBody = string.Empty;
			IOUtils.AddFunctionHeader( ref m_functionBody, "float4 ImpostorClipPosDepthBias( float4 clipPos, float4 viewPos, float depthSize )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "const float depthOffset = depthSize * 0.5 * length( AI_ObjectToWorld[ 2 ].xyz );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1.0 )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "// Orthographic" );
			IOUtils.AddFunctionLine( ref m_functionBody, "clipPos.z += UNITY_MATRIX_P[ 2 ][ 2 ] * depthOffset;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "else" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "// Perspective" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 viewPosAdjusted = float3( viewPos.xy, min( viewPos.z + depthOffset, -_ProjectionParams.y - 1e-4 ) );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float4 clipPosAdjusted = mul( UNITY_MATRIX_P, float4( viewPosAdjusted, 1 ) );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "clipPos.z = ( clipPosAdjusted.z / clipPosAdjusted.w ) * clipPos.w;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "return clipPos;" );
			IOUtils.CloseFunctionBody( ref m_functionBody );
		}

		private static string GenerateSamplingCall( ref MasterNodeDataCollector dataCollector, string propertyName, string uv, string biasName )
		{
			return GeneratorUtils.GenerateSamplingCall( ref dataCollector, WirePortDataType.SAMPLER2D, propertyName, "sampler" + propertyName, uv, MipType.MipBias, biasName );
		}

		private static string GenerateSamplingCall( ref MasterNodeDataCollector dataCollector, TexturePropertyNode property, string uv, RangedFloatNode bias )
		{
			return GeneratorUtils.GenerateSamplingCall( ref dataCollector, WirePortDataType.SAMPLER2D, property.PropertyName, "sampler" + property.PropertyName, uv, MipType.MipBias, bias.PropertyName );
		}

		private static string GenerateSamplingCall( ref MasterNodeDataCollector dataCollector, string property, string sampler, string uv, string bias )
		{
			return GeneratorUtils.GenerateSamplingCall( ref dataCollector, WirePortDataType.SAMPLER2D, property, sampler, uv, MipType.MipBias, bias );
		}

		private void GenerateSphereImpostorVertex( ref MasterNodeDataCollector dataCollector )
		{
			m_functionBody = string.Empty;
			IOUtils.AddFunctionHeader( ref m_functionBody, "inline void SphereImpostorVertex( inout float3 positionOS, out float3 normalOS, out float4 tangentOS, out float4 frameUVs, out float4 viewPos )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 uvOffset = _AI_SizeOffset.zw;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float sizeX = " + m_framesXProp.PropertyName + ";" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float sizeY = " + m_framesYProp.PropertyName + " - 1; " );
			IOUtils.AddFunctionLine( ref m_functionBody, "float UVscale = " + m_impostorSizeProp.PropertyName + ";" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float4 fractions = 1 / float4( sizeX, " + m_framesYProp.PropertyName + ", sizeY, UVscale );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 sizeFraction = fractions.xy;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float axisSizeFraction = fractions.z;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float fractionsUVscale = fractions.w;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 worldCameraPos;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "#if defined(UNITY_PASS_SHADOWCASTER)" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 worldOrigin = 0;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float4 perspective = float4( 0, 0, 0, 1 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "perspective = float4( 0, 0, 5000, 0 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "worldOrigin = AI_ObjectToWorld._m03_m13_m23;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "worldCameraPos = worldOrigin + mul( UNITY_MATRIX_I_V, perspective ).xyz;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
			IOUtils.AddFunctionLine( ref m_functionBody, "if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "worldCameraPos = AI_ObjectToWorld._m03_m13_m23 + UNITY_MATRIX_I_V._m02_m12_m22 * 5000;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "else" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			if ( dataCollector.IsSRP )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "worldCameraPos = GetCameraRelativePositionWS( _WorldSpaceCameraPos );" );
			}
			else
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined( SCENEPICKINGPASS ) || defined( SCENESELECTIONPASS )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "worldCameraPos = UNITY_MATRIX_I_V._m03_m13_m23;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "worldCameraPos = _WorldSpaceCameraPos;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 objectCameraPosition = mul( AI_WorldToObject, float4( worldCameraPos, 1 ) ).xyz - " + m_offsetProp.PropertyName + ".xyz; " );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 objectCameraDirection = normalize( objectCameraPosition );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 upVector = float3( 0,1,0 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 objectHorizontalVector = normalize( cross( objectCameraDirection, upVector ) );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 objectVerticalVector = cross( objectHorizontalVector, objectCameraDirection );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float verticalAngle = frac( atan2( -objectCameraDirection.z, -objectCameraDirection.x ) * AI_INV_TWO_PI ) * sizeX + 0.5;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float verticalDot = dot( objectCameraDirection, upVector );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float upAngle = ( acos( -verticalDot ) * AI_INV_PI ) + axisSizeFraction * 0.5f;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float yRot = sizeFraction.x * AI_PI * verticalDot * ( 2 * frac( verticalAngle ) - 1 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 uvExpansion = positionOS.xy;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float cosY = cos( yRot );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float sinY = sin( yRot );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 uvRotator = mul( uvExpansion, float2x2( cosY, -sinY, sinY, cosY ) );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 billboard = objectHorizontalVector * uvRotator.x + objectVerticalVector * uvRotator.y;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 relativeCoords = float2( floor( verticalAngle ), min( floor( upAngle * sizeY ), sizeY ) );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 frameUV = ( ( uvExpansion * fractionsUVscale + 0.5 ) + relativeCoords ) * sizeFraction;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "frameUVs.xy = frameUV - uvOffset;" );

			IOUtils.AddFunctionLine( ref m_functionBody, "#if defined( _USE_PARALLAX_ON )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 objectNormalVector = cross( objectHorizontalVector, -objectVerticalVector );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3x3 worldToLocal = float3x3( objectHorizontalVector, objectVerticalVector, objectNormalVector );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 sphereLocal = normalize( mul( worldToLocal, billboard - objectCameraPosition ) );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "frameUVs.zw = sphereLocal.xy * sizeFraction * " + m_parallaxProp.PropertyName + ";" );
			IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
			IOUtils.AddFunctionLine( ref m_functionBody, "frameUVs.zw = 0;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );

			IOUtils.AddFunctionLine( ref m_functionBody, "positionOS = billboard + " + m_offsetProp.PropertyName + ";" );
			IOUtils.AddFunctionLine( ref m_functionBody, "normalOS = objectCameraDirection;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "tangentOS = float4( objectHorizontalVector, 1 );" );

			if ( dataCollector.IsSRP )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz = TransformWorldToView( TransformObjectToWorld( positionOS ) );" );
			}
			else
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz = UnityObjectToViewPos( positionOS );" );
			}

			if ( m_speedTreeHueSupport )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#ifdef EFFECT_HUE_VARIATION" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float hueVariationAmount = frac( AI_ObjectToWorld[ 0 ].w + AI_ObjectToWorld[ 1 ].w + AI_ObjectToWorld[ 2 ].w );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.w = saturate( hueVariationAmount * " + m_hueProp.PropertyName + ".a );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.w = 0;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			else
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.w = 0;" );
			}

			IOUtils.CloseFunctionBody( ref m_functionBody );
		}

		private void GenerateSphereImpostorFragment( ref MasterNodeDataCollector dataCollector, bool withSpec = true, bool withOcclusion = true, bool withEmission = true, bool withPosition = false )
		{
			m_functionBody = string.Empty;

			string extraHeader = string.Empty;
			for ( int i = 0; i < m_extraSamplers; i++ )
			{
				if ( GetInputPortByArrayId( i ).IsConnected && GetOutputPortByArrayId( i ).IsConnected )
					extraHeader += ", out float4 output" + i;
			}

			IOUtils.AddFunctionHeader( ref m_functionBody, "inline void SphereImpostorFragment( inout ImpostorOutput o, out float4 positionCS, out float3 positionWS, float4 frameUV, float4 viewPos" + extraHeader + " )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "#if defined( _USE_PARALLAX_ON )");
			IOUtils.AddFunctionLine( ref m_functionBody, "float parallax = " + GenerateSamplingCall( ref dataCollector, m_normalTexture.PropertyName, "frameUV.xy", "-1" ) + ".a;" );

			IOUtils.AddFunctionLine( ref m_functionBody, "frameUV.xy = ( ( 0.5 - parallax ) * frameUV.zw ) + frameUV.xy;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float4 albedoSample = " + GenerateSamplingCall( ref dataCollector, m_albedoTexture, "frameUV.xy", m_textureBiasProp ) + ";" );

			IOUtils.AddFunctionLine( ref m_functionBody, "o.Alpha = ( albedoSample.a - " + m_clipProp.PropertyName + " );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "clip( o.Alpha );" );
			if ( m_speedTreeHueSupport )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#ifdef EFFECT_HUE_VARIATION" );
				IOUtils.AddFunctionLine( ref m_functionBody, "half3 shiftedColor = lerp( albedoSample.rgb, " + m_hueProp.PropertyName + ".rgb, viewPos.w );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "half maxBase = max( albedoSample.r, max( albedoSample.g, albedoSample.b ) );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "half newMaxBase = max( shiftedColor.r, max( shiftedColor.g, shiftedColor.b ) );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "maxBase /= newMaxBase;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "maxBase = maxBase * 0.5f + 0.5f;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "shiftedColor.rgb *= maxBase;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "albedoSample.rgb = saturate( shiftedColor );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			IOUtils.AddFunctionLine( ref m_functionBody, "o.Albedo = albedoSample.rgb;" );

			IOUtils.AddFunctionLine( ref m_functionBody, "float4 normalSample = " + GenerateSamplingCall( ref dataCollector, m_normalTexture, "frameUV.xy", m_textureBiasProp ) + ";" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float4 objectNormal = normalSample * 2 - 1;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "o.WorldNormal = normalize( mul( ( float3x3 )AI_ObjectToWorld, objectNormal.xyz ) );" );

			IOUtils.AddFunctionLine( ref m_functionBody, "const float objectScale = length( AI_ObjectToWorld[ 2 ].xyz );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float depth = ( normalSample.a - 0.5 ) * " + m_depthProp.PropertyName + " * objectScale;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "#if defined(UNITY_PASS_SHADOWCASTER) // Standard RP fix for deferred path" );
			IOUtils.AddFunctionLine( ref m_functionBody, "depth -= 0.001;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );

			if ( withSpec )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined( _SPECULARMAP )");
				IOUtils.AddFunctionLine( ref m_functionBody, "float4 specularSample = " + GenerateSamplingCall( ref dataCollector, m_specularTexture, "frameUV.xy", m_textureBiasProp ) + ";" );
				if ( m_workflow == ASEStandardSurfaceWorkflow.Specular )
					IOUtils.AddFunctionLine( ref m_functionBody, "o.Specular = specularSample.rgb;" );
				else
					IOUtils.AddFunctionLine( ref m_functionBody, "o.Metallic = saturate( ( specularSample.rgb - unity_ColorSpaceDielectricSpec.rgb ) / ( albedoSample.rgb - unity_ColorSpaceDielectricSpec.rgb ) ).g; " );
				IOUtils.AddFunctionLine( ref m_functionBody, "o.Smoothness = specularSample.a;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				if ( m_workflow == ASEStandardSurfaceWorkflow.Specular )
					IOUtils.AddFunctionLine( ref m_functionBody, "o.Specular = 0;" );
				else
					IOUtils.AddFunctionLine( ref m_functionBody, "o.Metallic = 0; " );
				IOUtils.AddFunctionLine( ref m_functionBody, "o.Smoothness = 0;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );

			}
			if ( withOcclusion )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined( _OCCLUSIONMAP )");
				IOUtils.AddFunctionLine( ref m_functionBody, "float4 occlusionSample = " + GenerateSamplingCall( ref dataCollector, m_occlusionTexture, "frameUV.xy", m_textureBiasProp ) + ";" );
				IOUtils.AddFunctionLine( ref m_functionBody, "o.Occlusion = occlusionSample.a;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "o.Occlusion = 1;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			if ( withEmission )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined( _EMISSIONMAP )");
				IOUtils.AddFunctionLine( ref m_functionBody, "float4 emissionSample = " + GenerateSamplingCall( ref dataCollector, m_emissionTexture, "frameUV.xy", m_textureBiasProp ) + ";" );
				IOUtils.AddFunctionLine( ref m_functionBody, "o.Emission = emissionSample.rgb;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "o.Emission = 0;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			if ( withPosition )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined( _POSITIONMAP )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float4 position = " + GenerateSamplingCall( ref dataCollector, m_positionTexture, "frameUV.xy", m_textureBiasProp ) + ";" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float3 objectPosition = position.xyz * _AI_BoundsSize + _AI_BoundsMin;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float3 worldPosition = mul( AI_ObjectToWorld, float4( objectPosition, 1 ) ).xyz;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "if ( position.a > 0 )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "{" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz = mul( UNITY_MATRIX_V, float4( worldPosition.xyz, 1 ) ).xyz;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "depth = 0;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "}" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}

			if ( dataCollector.IsSRP )
			{
				for ( int i = 0; i < DielecticSRPFix.Length; i++ )
				{
					dataCollector.AddToDirectives( DielecticSRPFix[ i ] );
				}
			}

			string customSamplerState = m_samplerStatePort.IsConnected ? m_samplerStatePort.GeneratePortInstructions( ref dataCollector ) : string.Empty;
			for ( int i = 0; i < m_extraSamplers; i++ )
			{
				if ( GetInputPortByArrayId( i ).IsConnected && GetOutputPortByArrayId( i ).IsConnected )
				{
					string samplerState = string.Empty;
					if ( m_samplerStatePort.IsConnected )
					{
						samplerState = customSamplerState;
					}
					else
					{
						TexturePropertyNode node = GetInputPortByArrayId( i ).GetOutputNodeWhichIsNotRelay() as TexturePropertyNode;
						if ( node != null )
						{
							samplerState = node.GenerateSamplerState( ref dataCollector );
						}
					}

					IOUtils.AddFunctionLine( ref m_functionBody, "output" + i + " = " + GenerateSamplingCall( ref dataCollector, m_extraPropertyNames[ i ], samplerState, "frameUV.xy", m_textureBiasProp.PropertyName ) + ";" );
				}
			}

			IOUtils.AddFunctionLine( ref m_functionBody, "float3 viewDir = ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 ) ? float3( 0, 0, 1 ) : normalize( -viewPos.xyz );" );
			if ( !dataCollector.IsSRP )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined( SHADOWS_DEPTH ) && !defined( SPOT ) && !defined( CUBE ) && !defined( DIRECTIONAL )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "if ( any( unity_LightShadowBias ) ) // get only the shadowcaster, this is a hack" );
				IOUtils.AddFunctionLine( ref m_functionBody, "{" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "}" );
				IOUtils.AddFunctionLine( ref m_functionBody, "else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "{" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "}" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#elif defined( UNITY_PASS_SHADOWCASTER )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			else
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if ( defined( SHADERPASS ) && ( ( defined( SHADERPASS_SHADOWS ) && SHADERPASS == SHADERPASS_SHADOWS ) || ( defined( SHADERPASS_SHADOWCASTER ) && SHADERPASS == SHADERPASS_SHADOWCASTER ) ) ) || defined( UNITY_PASS_SHADOWCASTER )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}

			IOUtils.AddFunctionLine( ref m_functionBody, "positionWS = mul( UNITY_MATRIX_I_V, float4( viewPos.xyz, 1 ) ).xyz;" );

			if ( !dataCollector.IsSRP )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "positionCS = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined(SHADOWS_DEPTH) && !defined( SPOT ) && !defined( CUBE ) && !defined( DIRECTIONAL )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "positionCS = UnityApplyLinearShadowBias( positionCS );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			else
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined(SHADERPASS) && defined(UNITY_PASS_SHADOWCASTER)" );
				if ( dataCollector.CurrentSRPType == TemplateSRPType.URP )
				{
					IOUtils.AddFunctionLine( ref m_functionBody, "#if _CASTING_PUNCTUAL_LIGHT_SHADOW" );
					IOUtils.AddFunctionLine( ref m_functionBody, "float3 lightDirectionWS = normalize( _LightPosition - positionWS );" );
					IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
					IOUtils.AddFunctionLine( ref m_functionBody, "float3 lightDirectionWS = _LightDirection;" );
					IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
					IOUtils.AddFunctionLine( ref m_functionBody, "positionCS = TransformWorldToHClip( ApplyShadowBias( positionWS, float3(0,0,0), lightDirectionWS ) );" );
				}
				else if ( dataCollector.CurrentSRPType == TemplateSRPType.HDRP )
				{
					// TODO HDRP
					IOUtils.AddFunctionLine( ref m_functionBody, "positionCS = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );" );
				}
				IOUtils.AddFunctionLine( ref m_functionBody, "#if UNITY_REVERSED_Z" );
				IOUtils.AddFunctionLine( ref m_functionBody, "positionCS.z = min( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "positionCS.z = max( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );

				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "positionCS = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			IOUtils.CloseFunctionBody( ref m_functionBody );
		}

		private void GenerateImpostorVertex( ref MasterNodeDataCollector dataCollector )
		{
			m_functionBody = string.Empty;
			IOUtils.AddFunctionHeader( ref m_functionBody, "inline void OctaImpostorVertex( inout float3 positionOS, out float3 normalOS, out float4 tangentOS, out float4 uvsFrame1, out float4 uvsFrame2, out float4 uvsFrame3, out float4 octaFrame, out float4 viewPos )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 uvOffset = _AI_SizeOffset.zw;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float parallax = -" + m_parallaxProp.PropertyName + "; " );
			IOUtils.AddFunctionLine( ref m_functionBody, "float UVscale = " + m_impostorSizeProp.PropertyName + ";" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float framesXY = " + m_framesProp.PropertyName + ";" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float prevFrame = framesXY - 1;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 fractions = 1.0 / float3( framesXY, prevFrame, UVscale );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float fractionsFrame = fractions.x;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float fractionsPrevFrame = fractions.y;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float fractionsUVscale = fractions.z;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 worldCameraPos;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "#if defined(UNITY_PASS_SHADOWCASTER)" );

			IOUtils.AddFunctionLine( ref m_functionBody, "float3 worldOrigin = 0;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float4 perspective = float4( 0, 0, 0, 1 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "perspective = float4( 0, 0, 5000, 0 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "worldOrigin = AI_ObjectToWorld._m03_m13_m23;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "worldCameraPos = worldOrigin + mul( UNITY_MATRIX_I_V, perspective ).xyz;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "#else" );

			// @diogo: not using UNITY_MATRIX_I_V here due to a unity bug sending slightly different matrices between depth-only and forward passes.
			IOUtils.AddFunctionLine( ref m_functionBody, "if ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			IOUtils.AddFunctionLine( ref m_functionBody, "worldCameraPos = AI_ObjectToWorld._m03_m13_m23 + UNITY_MATRIX_I_V._m02_m12_m22 * 5000;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );
			IOUtils.AddFunctionLine( ref m_functionBody, "else" );
			IOUtils.AddFunctionLine( ref m_functionBody, "{" );
			if ( dataCollector.IsSRP )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "worldCameraPos = GetCameraRelativePositionWS( _WorldSpaceCameraPos );" );
			}
			else
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "worldCameraPos = _WorldSpaceCameraPos;" );
			}
			IOUtils.AddFunctionLine( ref m_functionBody, "}" );

			IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 objectCameraPosition = mul( AI_WorldToObject, float4( worldCameraPos, 1 ) ).xyz - " + m_offsetProp.PropertyName + ".xyz; " );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 objectCameraDirection = normalize( objectCameraPosition );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 upVector = float3( 0,1,0 );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 objectHorizontalVector = normalize( cross( objectCameraDirection, upVector ) );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 objectVerticalVector = cross( objectHorizontalVector, objectCameraDirection );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 uvExpansion = positionOS.xy;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 billboard = objectHorizontalVector * uvExpansion.x + objectVerticalVector * uvExpansion.y;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 objectDir = billboard - objectCameraPosition; " );

			// @diogo: quantize to avoid a heinsenbug causing mismatching values between passes
			IOUtils.AddFunctionLine( ref m_functionBody, "objectCameraDirection = trunc( objectCameraDirection * 65536.0 ) / 65536.0;" );

			if ( m_hemiToggleSupport || IsCombinedOctahedron )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined( _HEMI_ON )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "objectCameraDirection.y = max( 0.001, objectCameraDirection.y );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float2 frameOcta = VectorToHemiOctahedron( objectCameraDirection.xzy ) * 0.5 + 0.5;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float2 frameOcta = VectorToOctahedron( objectCameraDirection.xzy ) * 0.5 + 0.5;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			else
			{
				if ( IsHemiOctahedron )
				{
					IOUtils.AddFunctionLine( ref m_functionBody, "objectCameraDirection.y = max(0.001, objectCameraDirection.y);" );
					IOUtils.AddFunctionLine( ref m_functionBody, "float2 frameOcta = VectorToHemiOctahedron( objectCameraDirection.xzy ) * 0.5 + 0.5;" );
				}
				else
				{
					IOUtils.AddFunctionLine( ref m_functionBody, "float2 frameOcta = VectorToOctahedron( objectCameraDirection.xzy ) * 0.5 + 0.5;" );
				}
			}

			IOUtils.AddFunctionLine( ref m_functionBody, "float2 prevOctaFrame = frameOcta * prevFrame;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 baseOctaFrame = floor( prevOctaFrame );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 fractionOctaFrame = ( baseOctaFrame * fractionsFrame );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 octaFrame1 = ( baseOctaFrame * fractionsPrevFrame ) * 2.0 - 1.0;" );

			if ( m_hemiToggleSupport || IsCombinedOctahedron )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined( _HEMI_ON )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float3 octa1WorldY = HemiOctahedronToVector( octaFrame1 ).xzy;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float3 octa1WorldY = OctahedronToVector( octaFrame1 ).xzy;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			else
			{
				if ( IsHemiOctahedron )
				{
					IOUtils.AddFunctionLine( ref m_functionBody, "float3 octa1WorldY = HemiOctahedronToVector( octaFrame1 ).xzy;" );
				}
				else
				{
					IOUtils.AddFunctionLine( ref m_functionBody, "float3 octa1WorldY = OctahedronToVector( octaFrame1 ).xzy;" );
				}
			}

			IOUtils.AddFunctionLine( ref m_functionBody, "float3 octa1LocalY;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 uvFrame1;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "RayPlaneIntersectionUV( octa1WorldY, objectCameraPosition, objectDir, /*out*/ uvFrame1, /*out*/ octa1LocalY );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 uvParallax1 = octa1LocalY.xy * fractionsFrame * parallax;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "uvFrame1 = ( uvFrame1 * fractionsUVscale + 0.5 ) * fractionsFrame + fractionOctaFrame;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "uvsFrame1 = float4( uvParallax1, uvFrame1 ) - float4( 0, 0, uvOffset );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 fractPrevOctaFrame = frac( prevOctaFrame );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 cornerDifference = lerp( float2( 0,1 ) , float2( 1,0 ) , saturate( ceil( ( fractPrevOctaFrame.x - fractPrevOctaFrame.y ) ) ));" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 octaFrame2 = ( ( baseOctaFrame + cornerDifference ) * fractionsPrevFrame ) * 2.0 - 1.0;" );

			if ( m_hemiToggleSupport || IsCombinedOctahedron )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined( _HEMI_ON )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float3 octa2WorldY = HemiOctahedronToVector( octaFrame2 ).xzy;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float3 octa2WorldY = OctahedronToVector( octaFrame2 ).xzy;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			else
			{
				if ( IsHemiOctahedron )
				{
					IOUtils.AddFunctionLine( ref m_functionBody, "float3 octa2WorldY = HemiOctahedronToVector( octaFrame2 ).xzy;" );
				}
				else
				{
					IOUtils.AddFunctionLine( ref m_functionBody, "float3 octa2WorldY = OctahedronToVector( octaFrame2 ).xzy;" );
				}
			}

			IOUtils.AddFunctionLine( ref m_functionBody, "float3 octa2LocalY;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 uvFrame2;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "RayPlaneIntersectionUV( octa2WorldY, objectCameraPosition, objectDir, /*out*/ uvFrame2, /*out*/ octa2LocalY );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 uvParallax2 = octa2LocalY.xy * fractionsFrame * parallax;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "uvFrame2 = ( uvFrame2 * fractionsUVscale + 0.5 ) * fractionsFrame + ( ( cornerDifference * fractionsFrame ) + fractionOctaFrame );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "uvsFrame2 = float4( uvParallax2, uvFrame2 ) - float4( 0, 0, uvOffset );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 octaFrame3 = ( ( baseOctaFrame + 1 ) * fractionsPrevFrame  ) * 2.0 - 1.0;" );

			if ( m_hemiToggleSupport || IsCombinedOctahedron )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined( _HEMI_ON )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float3 octa3WorldY = HemiOctahedronToVector( octaFrame3 ).xzy;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float3 octa3WorldY = OctahedronToVector( octaFrame3 ).xzy;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			else
			{
				if ( IsHemiOctahedron )
				{
					IOUtils.AddFunctionLine( ref m_functionBody, "float3 octa3WorldY = HemiOctahedronToVector( octaFrame3 ).xzy;" );
				}
				else
				{
					IOUtils.AddFunctionLine( ref m_functionBody, "float3 octa3WorldY = OctahedronToVector( octaFrame3 ).xzy;" );
				}
			}

			IOUtils.AddFunctionLine( ref m_functionBody, "float3 octa3LocalY;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 uvFrame3;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "RayPlaneIntersectionUV( octa3WorldY, objectCameraPosition, objectDir, /*out*/ uvFrame3, /*out*/ octa3LocalY );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 uvParallax3 = octa3LocalY.xy * fractionsFrame * parallax;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "uvFrame3 = ( uvFrame3 * fractionsUVscale + 0.5 ) * fractionsFrame + ( fractionOctaFrame + fractionsFrame );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "uvsFrame3 = float4( uvParallax3, uvFrame3 ) - float4( 0, 0, uvOffset );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "octaFrame = 0;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "octaFrame.xy = prevOctaFrame;" );
			if ( m_frameClampSupport )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined( AI_CLIP_NEIGHBOURS_FRAMES )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "octaFrame.zw = fractionOctaFrame;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}

			IOUtils.AddFunctionLine( ref m_functionBody, "positionOS = billboard + " + m_offsetProp.PropertyName + ";" );
			IOUtils.AddFunctionLine( ref m_functionBody, "normalOS = objectCameraDirection;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "tangentOS = float4( objectHorizontalVector, 1 );" );

			if ( dataCollector.IsSRP )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz = TransformWorldToView( TransformObjectToWorld( positionOS ) );" );
			}
			else
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz = UnityObjectToViewPos( positionOS );" );
			}

			if ( m_speedTreeHueSupport )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#ifdef EFFECT_HUE_VARIATION" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float hueVariationAmount = frac( AI_ObjectToWorld[ 0 ].w + AI_ObjectToWorld[ 1 ].w + AI_ObjectToWorld[ 2 ].w );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.w = saturate( hueVariationAmount * " + m_hueProp.PropertyName + ".a );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.w = 0;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			else
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.w = 0;" );
			}

			IOUtils.CloseFunctionBody( ref m_functionBody );
		}

		void GenerateImpostorFragment( ref MasterNodeDataCollector dataCollector, bool withSpec = true, bool withOcclusion = true, bool withEmission = true, bool withPosition = false )
		{
			m_functionBody = string.Empty;

			string extraHeader = string.Empty;
			for ( int i = 0; i < m_extraSamplers; i++ )
			{
				if ( GetInputPortByArrayId( i ).IsConnected && GetOutputPortByArrayId( i ).IsConnected )
					extraHeader += ", out float4 output" + i;
			}

			IOUtils.AddFunctionHeader( ref m_functionBody, "inline void OctaImpostorFragment( inout ImpostorOutput o, out float4 positionCS, out float3 positionWS, float4 uvsFrame1, float4 uvsFrame2, float4 uvsFrame3, float4 octaFrame, float4 viewPos" + extraHeader + " )" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 fraction = frac( octaFrame.xy );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 invFraction = 1 - fraction;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 weights;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "weights.x = min( invFraction.x, invFraction.y );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "weights.y = abs( fraction.x - fraction.y );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "weights.z = min( fraction.x, fraction.y );" );

			IOUtils.AddFunctionLine( ref m_functionBody, "float parallax1 = " + GenerateSamplingCall( ref dataCollector, m_normalTexture.PropertyName, "uvsFrame1.zw", "-1" ) + ".a;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float parallax2 = " + GenerateSamplingCall( ref dataCollector, m_normalTexture.PropertyName, "uvsFrame2.zw", "-1" ) + ".a;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float parallax3 = " + GenerateSamplingCall( ref dataCollector, m_normalTexture.PropertyName, "uvsFrame3.zw", "-1" ) + ".a;" );

			IOUtils.AddFunctionLine( ref m_functionBody, "float2 parallax1_uv = ( ( 0.5 - parallax1 ) * uvsFrame1.xy ) + uvsFrame1.zw;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 parallax2_uv = ( ( 0.5 - parallax2 ) * uvsFrame2.xy ) + uvsFrame2.zw;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float2 parallax3_uv = ( ( 0.5 - parallax3 ) * uvsFrame3.xy ) + uvsFrame3.zw;" );

			IOUtils.AddFunctionLine( ref m_functionBody, "float4 albedo1 = " + GenerateSamplingCall( ref dataCollector, m_albedoTexture, "parallax1_uv", m_textureBiasProp ) + ";" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float4 albedo2 = " + GenerateSamplingCall( ref dataCollector, m_albedoTexture, "parallax2_uv", m_textureBiasProp ) + ";" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float4 albedo3 = " + GenerateSamplingCall( ref dataCollector, m_albedoTexture, "parallax3_uv", m_textureBiasProp ) + ";" );

			IOUtils.AddFunctionLine( ref m_functionBody, "float4 blendedAlbedo = albedo1 * weights.x + albedo2 * weights.y + albedo3 * weights.z;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "o.Alpha = blendedAlbedo.a;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "clip( o.Alpha - " + m_clipProp.PropertyName + " );" );
			if ( m_frameClampSupport )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined( AI_CLIP_NEIGHBOURS_FRAMES )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float t = ceil( fraction.x - fraction.y );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float4 cornerDifference = float4( t, 1 - t, 1, 1 );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float2 step_1 = ( parallax1_uv - octaFrame.zw ) * " + m_framesProp.PropertyName + ";" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float4 step23 = ( float4( parallax2_uv, parallax3_uv ) -  octaFrame.zwzw ) * " + m_framesProp.PropertyName + " - cornerDifference;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "step_1 = step_1 * ( 1 - step_1 );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "step23 = step23 * ( 1 - step23 );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float3 steps;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "steps.x = step_1.x * step_1.y;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "steps.y = step23.x * step23.y;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "steps.z = step23.z * step23.w;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "steps = step(-steps, 0);" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float final = dot( steps, weights );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "clip( final - 0.5 );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			if ( m_speedTreeHueSupport )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#ifdef EFFECT_HUE_VARIATION" );
				IOUtils.AddFunctionLine( ref m_functionBody, "half3 shiftedColor = lerp( blendedAlbedo.rgb, " + m_hueProp.PropertyName + ".rgb, viewPos.w );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "half maxBase = max( blendedAlbedo.r, max( blendedAlbedo.g, blendedAlbedo.b ) );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "half newMaxBase = max( shiftedColor.r, max( shiftedColor.g, shiftedColor.b ) );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "maxBase /= newMaxBase;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "maxBase = maxBase * 0.5f + 0.5f;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "shiftedColor.rgb *= maxBase;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "blendedAlbedo.rgb = saturate( shiftedColor );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			IOUtils.AddFunctionLine( ref m_functionBody, "o.Albedo = blendedAlbedo.rgb;" );

			IOUtils.AddFunctionLine( ref m_functionBody, "float3 normal1 = " + GenerateSamplingCall( ref dataCollector, m_normalTexture, "parallax1_uv", m_textureBiasProp ) + ".rgb;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 normal2 = " + GenerateSamplingCall( ref dataCollector, m_normalTexture, "parallax2_uv", m_textureBiasProp ) + ".rgb;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 normal3 = " + GenerateSamplingCall( ref dataCollector, m_normalTexture, "parallax3_uv", m_textureBiasProp ) + ".rgb;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 blendedNormal = normal1 * weights.x  + normal2 * weights.y + normal3 * weights.z;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float3 objectNormal = blendedNormal * 2.0 - 1.0;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "o.WorldNormal = normalize( mul( ( float3x3 )AI_ObjectToWorld, objectNormal ) );" );

			IOUtils.AddFunctionLine( ref m_functionBody, "const float objectScale = length( AI_ObjectToWorld[ 2 ].xyz );" );
			IOUtils.AddFunctionLine( ref m_functionBody, "float depth = parallax1 * weights.x + parallax2 * weights.y + parallax3 * weights.z;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "depth = ( depth - 0.5 ) * " + m_depthProp.PropertyName + " * objectScale;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "#if defined(UNITY_PASS_SHADOWCASTER) // Standard RP fix for deferred path" );
			IOUtils.AddFunctionLine( ref m_functionBody, "depth -= 0.001;" );
			IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );

			if ( withSpec )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined( _SPECULARMAP )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float4 specular1 = " + GenerateSamplingCall( ref dataCollector, m_specularTexture, "parallax1_uv", m_textureBiasProp ) + ";" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float4 specular2 = " + GenerateSamplingCall( ref dataCollector, m_specularTexture, "parallax2_uv", m_textureBiasProp ) + ";" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float4 specular3 = " + GenerateSamplingCall( ref dataCollector, m_specularTexture, "parallax3_uv", m_textureBiasProp ) + ";" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float4 blendedSpecular = specular1 * weights.x  + specular2 * weights.y + specular3 * weights.z;" );

				if ( m_workflow == ASEStandardSurfaceWorkflow.Specular )
					IOUtils.AddFunctionLine( ref m_functionBody, "o.Specular = blendedSpecular.rgb;" );
				else
					IOUtils.AddFunctionLine( ref m_functionBody, "o.Metallic = saturate( ( blendedSpecular.rgb - unity_ColorSpaceDielectricSpec.rgb ) / ( blendedAlbedo.rgb - unity_ColorSpaceDielectricSpec.rgb ) ).g; " );

				IOUtils.AddFunctionLine( ref m_functionBody, "o.Smoothness = blendedSpecular.a;" );

				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				if ( m_workflow == ASEStandardSurfaceWorkflow.Specular )
					IOUtils.AddFunctionLine( ref m_functionBody, "o.Specular = 0;" );
				else
					IOUtils.AddFunctionLine( ref m_functionBody, "o.Metallic = 0; " );
				IOUtils.AddFunctionLine( ref m_functionBody, "o.Smoothness = 0;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			if ( withOcclusion )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined( _OCCLUSIONMAP )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float occlusion1 = " + GenerateSamplingCall( ref dataCollector, m_occlusionTexture, "parallax1_uv", m_textureBiasProp ) + ".g;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float occlusion2 = " + GenerateSamplingCall( ref dataCollector, m_occlusionTexture, "parallax2_uv", m_textureBiasProp ) + ".g;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float occlusion3 = " + GenerateSamplingCall( ref dataCollector, m_occlusionTexture, "parallax3_uv", m_textureBiasProp ) + ".g;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "o.Occlusion = occlusion1 * weights.x  + occlusion2 * weights.y + occlusion3 * weights.z;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "o.Occlusion = 1;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			if ( withEmission )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined( _EMISSIONMAP )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float3 emission1 = " + GenerateSamplingCall( ref dataCollector, m_emissionTexture, "parallax1_uv", m_textureBiasProp ) + ".rgb;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float3 emission2 = " + GenerateSamplingCall( ref dataCollector, m_emissionTexture, "parallax2_uv", m_textureBiasProp ) + ".rgb;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float3 emission3 = " + GenerateSamplingCall( ref dataCollector, m_emissionTexture, "parallax3_uv", m_textureBiasProp ) + ".rgb;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "o.Emission = emission1 * weights.x  + emission2 * weights.y + emission3 * weights.z;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "o.Emission = 0;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			if ( withPosition )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined( _POSITIONMAP )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float4 position1 = " + GenerateSamplingCall( ref dataCollector, m_positionTexture, "parallax1_uv", m_textureBiasProp ) + ";" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float4 position2 = " + GenerateSamplingCall( ref dataCollector, m_positionTexture, "parallax2_uv", m_textureBiasProp ) + ";" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float4 position3 = " + GenerateSamplingCall( ref dataCollector, m_positionTexture, "parallax3_uv", m_textureBiasProp ) + ";" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float4 blendedPosition = position1 * weights.x  + position2 * weights.y + position3 * weights.z;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float3 objectPosition = blendedPosition.xyz * _AI_BoundsSize + _AI_BoundsMin;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "float3 worldPosition = mul( AI_ObjectToWorld, float4( objectPosition, 1 ) ).xyz;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "if ( blendedPosition.a > 0 )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "{" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz = mul( UNITY_MATRIX_V, float4( worldPosition.xyz, 1 ) ).xyz;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "depth = 0;" );
				IOUtils.AddFunctionLine( ref m_functionBody, "}" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}

			if ( dataCollector.IsSRP )
			{
				for ( int i = 0; i < DielecticSRPFix.Length; i++ )
				{
					dataCollector.AddToDirectives( DielecticSRPFix[ i ] );
				}
			}

			string customSamplerState = m_samplerStatePort.IsConnected ? m_samplerStatePort.GeneratePortInstructions( ref dataCollector ) : string.Empty;
			for ( int i = 0; i < m_extraSamplers; i++ )
			{
				if ( GetInputPortByArrayId( i ).IsConnected && GetOutputPortByArrayId( i ).IsConnected )
				{
					string samplerState = string.Empty;
					if ( m_samplerStatePort.IsConnected )
					{
						samplerState = customSamplerState;
					}
					else
					{
						TexturePropertyNode node = GetInputPortByArrayId( i ).GetOutputNodeWhichIsNotRelay() as TexturePropertyNode;
						if ( node != null )
						{
							samplerState = node.GenerateSamplerState( ref dataCollector );
						}
					}

					IOUtils.AddFunctionLine( ref m_functionBody, "float4 output" + i + "a = " + GenerateSamplingCall( ref dataCollector, m_extraPropertyNames[ i ], samplerState, "parallax1_uv", m_textureBiasProp.PropertyName ) + ";" );
					IOUtils.AddFunctionLine( ref m_functionBody, "float4 output" + i + "b = " + GenerateSamplingCall( ref dataCollector, m_extraPropertyNames[ i ], samplerState, "parallax2_uv", m_textureBiasProp.PropertyName ) + ";" );
					IOUtils.AddFunctionLine( ref m_functionBody, "float4 output" + i + "c = " + GenerateSamplingCall( ref dataCollector, m_extraPropertyNames[ i ], samplerState, "parallax3_uv", m_textureBiasProp.PropertyName ) + ";" );
					IOUtils.AddFunctionLine( ref m_functionBody, "output" + i + " = output" + i + "a * weights.x  + output" + i + "b * weights.y + output" + i + "c * weights.z; " );
				}
			}

			IOUtils.AddFunctionLine( ref m_functionBody, "float3 viewDir = ( UNITY_MATRIX_P[ 3 ][ 3 ] == 1 ) ? float3( 0, 0, 1 ) : normalize( -viewPos.xyz );" );
			if ( !dataCollector.IsSRP )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined( SHADOWS_DEPTH ) && !defined( SPOT ) && !defined( CUBE ) && !defined( DIRECTIONAL )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "if ( any( unity_LightShadowBias ) ) // get only the shadowcaster, this is a hack" );
				IOUtils.AddFunctionLine( ref m_functionBody, "{" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "}" );
				IOUtils.AddFunctionLine( ref m_functionBody, "else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "{" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "}" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#elif defined( UNITY_PASS_SHADOWCASTER )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale  );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			else
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if ( defined( SHADERPASS ) && ( ( defined( SHADERPASS_SHADOWS ) && SHADERPASS == SHADERPASS_SHADOWS ) || ( defined( SHADERPASS_SHADOWCASTER ) && SHADERPASS == SHADERPASS_SHADOWCASTER ) ) ) || defined( UNITY_PASS_SHADOWCASTER )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz += viewDir * ( depth * _AI_ShadowView - _AI_ShadowBias * objectScale );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "viewPos.xyz += viewDir * ( depth + _AI_ForwardBias * objectScale );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}

			IOUtils.AddFunctionLine( ref m_functionBody, "positionWS = mul( UNITY_MATRIX_I_V, float4( viewPos.xyz, 1 ) ).xyz;" );

			if ( !dataCollector.IsSRP )
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "positionCS = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined(SHADOWS_DEPTH) && !defined( SPOT ) && !defined( CUBE ) && !defined( DIRECTIONAL )" );
				IOUtils.AddFunctionLine( ref m_functionBody, "positionCS = UnityApplyLinearShadowBias( positionCS );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			else
			{
				IOUtils.AddFunctionLine( ref m_functionBody, "#if defined(SHADERPASS) && defined(UNITY_PASS_SHADOWCASTER)" );
				if ( dataCollector.CurrentSRPType == TemplateSRPType.URP )
				{
					IOUtils.AddFunctionLine( ref m_functionBody, "#if _CASTING_PUNCTUAL_LIGHT_SHADOW" );
					IOUtils.AddFunctionLine( ref m_functionBody, "float3 lightDirectionWS = normalize( _LightPosition - positionWS );" );
					IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
					IOUtils.AddFunctionLine( ref m_functionBody, "float3 lightDirectionWS = _LightDirection;" );
					IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
					IOUtils.AddFunctionLine( ref m_functionBody, "positionCS = TransformWorldToHClip( ApplyShadowBias( positionWS, float3( 0, 0, 0 ), lightDirectionWS ) );" );
				}
				else if ( dataCollector.CurrentSRPType == TemplateSRPType.HDRP )
				{
					// TODO HDRP
					IOUtils.AddFunctionLine( ref m_functionBody, "positionCS = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );" );
				}
				IOUtils.AddFunctionLine( ref m_functionBody, "#if UNITY_REVERSED_Z" );
				IOUtils.AddFunctionLine( ref m_functionBody, "positionCS.z = min( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "positionCS.z = max( positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );

				IOUtils.AddFunctionLine( ref m_functionBody, "#else" );
				IOUtils.AddFunctionLine( ref m_functionBody, "positionCS = mul( UNITY_MATRIX_P, float4( viewPos.xyz, 1 ) );" );
				IOUtils.AddFunctionLine( ref m_functionBody, "#endif" );
			}
			IOUtils.CloseFunctionBody( ref m_functionBody );
		}
	}
}
#endif
