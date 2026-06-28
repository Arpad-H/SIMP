// Amplify Impostors
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

using System.IO;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
#endif
using UnityEngine;

namespace AmplifyImpostors
{
	public class Preferences
	{
	#if UNITY_EDITOR
		public enum ShowOption
		{
			Always = 0,
			OnNewVersion = 1,
			Never = 2
		}

		private static readonly GUIContent StartUp = new GUIContent( "Show start screen on Unity launch", "You can set if you want to see the start screen everytime Unity launches, only just when there's a new version available or never." );
		private static readonly GUIContent AutoSRP = new GUIContent( "Auto import SRP shaders", "By default Amplify Impostors checks for your SRP version and automatically imports compatible shaders.\nTurn this OFF if you prefer to import them manually." );
		private static readonly GUIContent DefineSymbol = new GUIContent( "Add Amplify Impostors define symbol", "Turning it OFF will disable the automatic insertion of the define symbol and remove it from the list while turning it ON will do the opposite.\nThis is used for compatibility with other plugins, if you are not sure if you need this leave it ON." );

		public static readonly string PrefGlobalFolder = "IMPOSTORS_GLOBALFOLDER";
		public static readonly string PrefGlobalRelativeFolder = "IMPOSTORS_GLOBALRELATIVEFOLDER";
		public static readonly string PrefGlobalDefault = "IMPOSTORS_GLOBALDEFAULT";
		public static readonly string PrefGlobalTexImport = "IMPOSTORS_GLOBALTEXIMPORT";
		public static readonly string PrefGlobalCreateLodGroup = "IMPOSTORS_GLOBALCREATELODGROUP ";
		public static readonly string PrefGlobalAlbedoName = "IMPOSTORS_GLOBALGBUFFER0SUFFIX";
		public static readonly string PrefGlobalSpecularName = "IMPOSTORS_GLOBALGBUFFER1SUFFIX";
		public static readonly string PrefGlobalNormalsName = "IMPOSTORS_GLOBALGBUFFER2SUFFIX";
		public static readonly string PrefGlobalEmissionName = "IMPOSTORS_GLOBALGBUFFER3SUFFIX";
		public static readonly string PrefGlobalOcclusionName = "IMPOSTORS_GLOBALGBUFFER4SUFFIX";
		public static readonly string PrefGlobalPositionName = "IMPOSTORS_GLOBALGBUFFER5SUFFIX";
		public static readonly string PrefGlobalBakingOptions = "IMPOSTORS_GLOBALBakingOptions";
		public static readonly string PrefGlobalStartUp = "IMPOSTORS_GLOBALSTARTUP";
		public static readonly string PrefGlobalAutoSRP = "IMPOSTORS_GLOBALAUTOSRP";
		public static readonly string PrefGlobalDefineSymbol = "IMPOSTORS_GLOBALDEFINESYMBOL";
		public static readonly string PrefGlobalBakePresetGUID = "IMPOSTORS_GLOBALBAKEPRESETGUID";

		public static readonly string PrefDataImpType = "IMPOSTORS_DATAIMPTYPE";
		public static readonly string PrefDataTexSizeLocked = "IMPOSTORS_DATATEXSIZEXLOCKED";
		public static readonly string PrefDataTexSizeSelected = "IMPOSTORS_DATATEXSIZEXSELECTED";
		public static readonly string PrefDataTexSizeX = "IMPOSTORS_DATATEXSIZEX";
		public static readonly string PrefDataTexSizeY = "IMPOSTORS_DATATEXSIZEY";
		public static readonly string PrefDataDecoupledFrames = "IMPOSTORS_DATADECOUPLEDFRAMES";
		public static readonly string PrefDataXFrames = "IMPOSTORS_DATAXFRAMES";
		public static readonly string PrefDataYFrames = "IMPOSTORS_DATAYFRAMES";
		public static readonly string PrefDataPixelBleeding = "IMPOSTORS_DATAPIXELBLEEDING";

		public static readonly string PrefDataTolerance = "IMPOSTORS_DATATOLERANCE ";
		public static readonly string PrefDataNormalScale = "IMPOSTORS_DATANORMALSCALE";
		public static readonly string PrefDataMaxVertices = "IMPOSTORS_DATAMAXVERTICES";

		public static readonly string DefaultAlbedoName = "_AlbedoAlpha";
		public static readonly string DefaultSpecularName = "_SpecularSmoothness";
		public static readonly string DefaultNormalName = "_NormalDepth";
		public static readonly string DefaultEmissionName = "_EmissionOcclusion";
		public static readonly string DefaultOcclusionName = "_Occlusion";
		public static readonly string DefaultPositionName = "_Position";

		public static readonly string DefaultBakePresetGUID = "e4786beb7716da54dbb02a632681cc37";

		public static bool GlobalDefaultMode = EditorPrefs.GetBool( PrefGlobalDefault, false );
		public static string GlobalFolder = EditorPrefs.GetString( PrefGlobalFolder, "" );
		public static string GlobalRelativeFolder = EditorPrefs.GetString( PrefGlobalRelativeFolder, "" );
		public static int GlobalTexImport = EditorPrefs.GetInt( PrefGlobalTexImport, 0 );
		public static bool GlobalCreateLodGroup = EditorPrefs.GetBool( PrefGlobalCreateLodGroup, false );
		public static string GlobalAlbedo = EditorPrefs.GetString( PrefGlobalAlbedoName, DefaultAlbedoName );
		public static string GlobalNormals = EditorPrefs.GetString( PrefGlobalSpecularName, DefaultNormalName );
		public static string GlobalSpecular = EditorPrefs.GetString( PrefGlobalNormalsName, DefaultSpecularName );
		public static string GlobalOcclusion = EditorPrefs.GetString( PrefGlobalEmissionName, DefaultOcclusionName );
		public static string GlobalEmission = EditorPrefs.GetString( PrefGlobalOcclusionName, DefaultEmissionName );
		public static string GlobalPosition = EditorPrefs.GetString( PrefGlobalPositionName, DefaultPositionName );
		public static bool GlobalBakingOptions = EditorPrefs.GetBool( PrefGlobalBakingOptions, true );
		public static ShowOption GlobalStartUp = ( ShowOption )EditorPrefs.GetInt( PrefGlobalStartUp, 0 );
		public static bool GlobalAutoSRP = EditorPrefs.GetBool( PrefGlobalAutoSRP, true );
		public static bool GlobalDefineSymbol = EditorPrefs.GetBool( PrefGlobalDefineSymbol, true );
		public static string GlobalBakePresetGUID = EditorPrefs.GetString( PrefGlobalBakePresetGUID, DefaultBakePresetGUID );

		private static readonly GUIContent DefaultSettingsLabel = new GUIContent( "New Impostor Defaults", "Default settings to be assigned to new Impostors" );
		private static readonly GUIContent DefaultBakePresetLabel = new GUIContent( "New Bake Preset Defaults", "Default Suffixes for new Bake Presets" );

		private static bool PrefsLoaded = false;
		private static GUIContent PathButtonContent = new GUIContent();

		[SettingsProvider]
		public static SettingsProvider ImpostorsSettings()
		{
			var provider = new SettingsProvider( "Preferences/Amplify Impostors", SettingsScope.User )
			{
				guiHandler = ( string searchContext ) => {
					PreferencesGUI();
				}
			};
			return provider;
		}

		public static void PreferencesGUI()
		{
			if ( !PrefsLoaded )
			{
				LoadDefaults();
				PrefsLoaded = true;
			}

			PathButtonContent.text = string.IsNullOrEmpty( GlobalFolder ) ? "Click to select folder" : GlobalFolder;

			EditorGUIUtility.labelWidth = 250;

			GlobalStartUp = ( ShowOption )EditorGUILayout.EnumPopup( StartUp, GlobalStartUp );
			GlobalAutoSRP = EditorGUILayout.Toggle( AutoSRP, GlobalAutoSRP );
			GlobalDefineSymbol = EditorGUILayout.Toggle( DefineSymbol, GlobalDefineSymbol );

			GlobalTexImport = EditorGUILayout.Popup( "Texture Importer Settings", GlobalTexImport, new string[] { "Ask if resolution is different", "Don't ask, always change", "Don't ask, never change" } );
			GlobalCreateLodGroup = EditorGUILayout.Toggle( "Create LODGroup if not present", GlobalCreateLodGroup );
			GUILayout.Space( 5 );

			GUILayout.Label( DefaultSettingsLabel, "boldlabel" );

			GlobalDefaultMode = ( FolderMode )EditorGUILayout.EnumPopup( "Default Path", GlobalDefaultMode ? FolderMode.Global : FolderMode.RelativeToPrefab ) == FolderMode.Global;
			EditorGUILayout.BeginHorizontal();
			if ( GlobalDefaultMode )
			{
				EditorGUI.BeginChangeCheck();
				GlobalFolder = EditorGUILayout.TextField( "    Global Folder", GlobalFolder );
				if ( EditorGUI.EndChangeCheck() )
				{
					GlobalFolder = GlobalFolder.TrimStart( new char[] { '/', '*', '.', ' ' } );
					GlobalFolder = "/" + GlobalFolder;
					GlobalFolder = GlobalFolder.TrimEnd( new char[] { '/', '*', '.', ' ' } );
					EditorPrefs.SetString( PrefGlobalFolder, GlobalFolder );
				}
				if ( GUILayout.Button( "...", "minibutton", GUILayout.Width( 20 )/*GUILayout.MaxWidth( Screen.width * 0.5f )*/ ) )
				{
					string oneLevelUp = Application.dataPath + "/../";
					string directory = Path.GetFullPath( oneLevelUp ).Replace( "\\", "/" );
					string fullpath = directory + GlobalFolder;
					string folderpath = EditorUtility.SaveFolderPanel( "Save Impostor to folder", FileUtil.GetProjectRelativePath( fullpath ), null );

					folderpath = FileUtil.GetProjectRelativePath( folderpath );
					if ( !string.IsNullOrEmpty( folderpath ) )
					{
						GlobalFolder = folderpath;
						GlobalFolder = GlobalFolder.TrimStart( new char[] { '/', '*', '.', ' ' } );
						GlobalFolder = "/" + GlobalFolder;
						GlobalFolder = GlobalFolder.TrimEnd( new char[] { '/', '*', '.', ' ' } );
						EditorPrefs.SetString( PrefGlobalFolder, GlobalFolder );
					}
				}
			}
			else
			{
				EditorGUI.BeginChangeCheck();
				GlobalRelativeFolder = EditorGUILayout.TextField( "    Relative to Prefab Folder", GlobalRelativeFolder );
				if ( EditorGUI.EndChangeCheck() )
				{
					GlobalRelativeFolder = GlobalRelativeFolder.TrimStart( new char[] { '/', '*', '.', ' ' } );
					GlobalRelativeFolder = "/" + GlobalRelativeFolder;
					GlobalRelativeFolder = GlobalRelativeFolder.TrimEnd( new char[] { '/', '*', '.', ' ' } );
					EditorPrefs.SetString( PrefGlobalRelativeFolder, GlobalRelativeFolder );
				}
				EditorGUI.BeginDisabledGroup( true );
				GUILayout.Button( "...", "minibutton", GUILayout.Width( 20 ) );
				EditorGUI.EndDisabledGroup();
			}
			EditorGUILayout.EndHorizontal();

			var bakePreset = AssetDatabase.LoadAssetAtPath<AmplifyImpostorBakePreset>( AssetDatabase.GUIDToAssetPath( GlobalBakePresetGUID ) );
			var newBakePreset = ( AmplifyImpostorBakePreset )EditorGUILayout.ObjectField( "Default Bake Preset", bakePreset, typeof( AmplifyImpostorBakePreset ), false );
			if ( newBakePreset != bakePreset )
			{
				GlobalBakePresetGUID = ( newBakePreset != null ) ? AssetDatabase.GUIDFromAssetPath( AssetDatabase.GetAssetPath( newBakePreset ) ).ToString() : string.Empty;
				Debug.Log( AssetDatabase.GUIDToAssetPath( GlobalBakePresetGUID ) );
			}

			GUILayout.Space( 5 );

			GUILayout.Label( DefaultBakePresetLabel, "boldlabel" );
			GlobalAlbedo = EditorGUILayout.TextField( "Albedo (RGB) Alpha (A)", GlobalAlbedo );
			GlobalNormals = EditorGUILayout.TextField( "Normal (RGB) Depth (A)", GlobalNormals );
			GlobalSpecular = EditorGUILayout.TextField( "Specular (RGB) Smoothness (A)", GlobalSpecular );
			GlobalOcclusion = EditorGUILayout.TextField( "Occlusion (RGB)", GlobalOcclusion );
			GlobalEmission = EditorGUILayout.TextField( "Emission (RGB)", GlobalEmission );
			GlobalPosition = EditorGUILayout.TextField( "Position (RGB)", GlobalPosition );

			if ( GUI.changed )
			{
				EditorPrefs.SetInt( PrefGlobalStartUp, ( int )GlobalStartUp );
				EditorPrefs.SetBool( PrefGlobalAutoSRP, GlobalAutoSRP );
				EditorPrefs.SetBool( PrefGlobalDefineSymbol, GlobalDefineSymbol );
				EditorPrefs.SetInt( PrefGlobalTexImport, GlobalTexImport );
				EditorPrefs.SetBool( PrefGlobalCreateLodGroup, GlobalCreateLodGroup );

				EditorPrefs.SetBool( PrefGlobalDefault, GlobalDefaultMode );
				EditorPrefs.SetString( PrefGlobalBakePresetGUID, GlobalBakePresetGUID );

				EditorPrefs.SetString( PrefGlobalAlbedoName, GlobalAlbedo );
				EditorPrefs.SetString( PrefGlobalSpecularName, GlobalSpecular );
				EditorPrefs.SetString( PrefGlobalNormalsName, GlobalNormals );
				EditorPrefs.SetString( PrefGlobalEmissionName, GlobalEmission );
				EditorPrefs.SetString( PrefGlobalOcclusionName, GlobalOcclusion );
				EditorPrefs.SetString( PrefGlobalPositionName, GlobalPosition );

				if ( GlobalDefineSymbol )
				{
					SetSymbolOnBuildTargetGroup( EditorUserBuildSettings.selectedBuildTargetGroup );
				}
				else
				{
					RemoveSymbolOnBuildTargetGroup( EditorUserBuildSettings.selectedBuildTargetGroup );
				}
			}
		}

		public static void LoadDefaults()
		{
			GlobalStartUp = ( ShowOption )EditorPrefs.GetInt( PrefGlobalStartUp, 0 );
			GlobalAutoSRP =  EditorPrefs.GetBool( PrefGlobalAutoSRP, true );
			GlobalDefineSymbol =  EditorPrefs.GetBool( PrefGlobalDefineSymbol, true );

			GlobalTexImport = EditorPrefs.GetInt( PrefGlobalTexImport, 0 );
			GlobalCreateLodGroup = EditorPrefs.GetBool( PrefGlobalCreateLodGroup, false );
			GlobalBakingOptions = EditorPrefs.GetBool( PrefGlobalBakingOptions, true );

			GlobalFolder = EditorPrefs.GetString( PrefGlobalFolder, "" );
			GlobalRelativeFolder = EditorPrefs.GetString( PrefGlobalRelativeFolder, "" );
			GlobalDefaultMode = EditorPrefs.GetBool( PrefGlobalDefault, false );
			GlobalBakePresetGUID = EditorPrefs.GetString( PrefGlobalBakePresetGUID, DefaultBakePresetGUID );

			GlobalAlbedo = EditorPrefs.GetString( PrefGlobalAlbedoName, DefaultAlbedoName );
			GlobalSpecular = EditorPrefs.GetString( PrefGlobalSpecularName, DefaultSpecularName );
			GlobalNormals = EditorPrefs.GetString( PrefGlobalNormalsName, DefaultNormalName );
			GlobalEmission = EditorPrefs.GetString( PrefGlobalEmissionName, DefaultEmissionName );
			GlobalOcclusion = EditorPrefs.GetString( PrefGlobalOcclusionName, DefaultOcclusionName );
			GlobalPosition = EditorPrefs.GetString( PrefGlobalPositionName, DefaultPositionName );
		}

		// Scripting Define Symbol Helper
		private static readonly string AmplifyImpostorsDefineSymbol = "AMPLIFY_IMPOSTORS";
		private static bool Initialized = false;

		[InitializeOnLoadMethod]
		public static void Init()
		{
			if( !Initialized )
			{
				Initialized = true;
				if ( Preferences.GlobalDefineSymbol )
				{
					SetSymbolOnBuildTargetGroup( EditorUserBuildSettings.selectedBuildTargetGroup );
				}
			}
		}

		public static void SetSymbolOnBuildTargetGroup( BuildTargetGroup targetGroup )
		{
			var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup( targetGroup );
			string currData = PlayerSettings.GetScriptingDefineSymbols( namedBuildTarget );
			if( !currData.Contains( AmplifyImpostorsDefineSymbol ) )
			{
				if( string.IsNullOrEmpty( currData ) )
				{
					PlayerSettings.SetScriptingDefineSymbols( namedBuildTarget, AmplifyImpostorsDefineSymbol );
				}
				else
				{
					if( !currData[ currData.Length - 1 ].Equals( ';' ) )
					{
						currData += ';';
					}
					currData += AmplifyImpostorsDefineSymbol;

					PlayerSettings.SetScriptingDefineSymbols( namedBuildTarget, currData );
				}
			}
		}

		public static void RemoveSymbolOnBuildTargetGroup( BuildTargetGroup targetGroup )
		{
			var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup( targetGroup );
			string currData = PlayerSettings.GetScriptingDefineSymbols( namedBuildTarget );
			if( currData.Contains( AmplifyImpostorsDefineSymbol ) )
			{
				currData = currData.Replace( AmplifyImpostorsDefineSymbol + ";", "" );
				currData = currData.Replace( ";" + AmplifyImpostorsDefineSymbol, "" );
				currData = currData.Replace( AmplifyImpostorsDefineSymbol, "" );

				PlayerSettings.SetScriptingDefineSymbols( namedBuildTarget, currData );
			}
		}
	#endif
	}
}
