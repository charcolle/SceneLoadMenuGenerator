using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace charcolle.Utility {

    public static class SceneLoadMenuGenerator {

        //===========================================
        // Varies
        //===========================================

        private const string SCENELOADMENU_FILENAME    = "SceneLoadMenu.cs";
        private const string SEARCH_TIHS_SCRIPT        = "SceneLoadMenuGenerator";
        private const string SEARCH_SCENE_FILTER       = "t:Scene";

        private const string PATTERN_SCENENAME_INVALID = @"^[!-/:-@¥[-`{-~]+$";

        private const string TEMPLATE_CLASS = @"using UnityEditor;
using UnityEditor.SceneManagement;
namespace charcolle.Utility {

    public static class SceneLoadMenu {
    #METHODS#
    }
}
";
        private const string TEMPLATE_LOAD = @"
        [MenuItem( #SCENEMENU#, false, #PRIORITY# )]
        static void Open_#SCENENAME#() {
            if ( EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() ) {
                EditorSceneManager.OpenScene( #SCENEPATH#, OpenSceneMode.Single );
            }
        }
";
        private const string TEMPLATE_ADD = @"
        [MenuItem( #SCENEMENU#, false, #PRIORITY# )]
        static void Open_#SCENENAME#() {
            if ( EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() ) {
                EditorSceneManager.OpenScene( #SCENEPATH#, OpenSceneMode.Additive );
            }
        }
";
        private const string MENU_NORMAL       = "\"Scenes/Load Scene/Scene/#SCENENAME#\"";
        private const string MENU_BUILD        = "\"Scenes/Load Scene/Build/#SCENENAME#\"";
        private const string MENU_TEST         = "\"Scenes/Load Scene/Test/#SCENENAME#\"";
        private const string MENU_ADD          = "\"Scenes/Add Scene/#SCENENAME#\"";

        private const string PRIORITY_LOAD     = "23";
        private const string PRIORITY_ADD      = "12";
        private const string PRIORITY_BUILD    = "34";

        private const string REPLACE_METHOD    = "#METHODS#";
        private const string REPLACE_MENU      = "#SCENEMENU#";
        private const string REPLACE_NAME      = "#SCENENAME#";
        private const string REPLACE_PATH      = "#SCENEPATH#";
        private const string REPLACE_PRIORITY  = "#PRIORITY#";

        private const string KEY_BUILDSCENEPATH = "charcolle.SceneLoadMenu.BuildScenePath";
        private const string KEY_FIRSTSCENEPATH = "charcolle.SceneLoadMenu.FirstScenePath";

        private static string m_buildScenePath;
        private static string m_firstScenePath;

        //===========================================
        // Generate Scenes Menu
        //===========================================

        [MenuItem( "Scenes/Generate SceneMenu", false, 1 )]
        static void GenerateScenLoadMenu() {

            registerBuildScene();
            generateScenLoadMenuProcess();

        }

        private static void generateScenLoadMenuProcess() {
            // load scenes from project and cache at list
            var sceneList = getScenesFromProject();

            if ( sceneList == null || sceneList.Count == 0 ) {
                Debug.LogWarning( "no scenes is found." );
                return;
            }

            // create methods( load/scenes, add, test, build  )
            var methods = generateMethods( sceneList );

            // save files
            if ( !string.IsNullOrEmpty( methods ) )
                saveSceneLoadMenuScript( methods );
        }


        [PreferenceItem( "SceneLoadMenu" )]
        private static void OnPreferenceGUI() {

            m_buildScenePath = EditorPrefs.GetString( KEY_BUILDSCENEPATH, "" );
            m_firstScenePath = EditorPrefs.GetString( KEY_FIRSTSCENEPATH, "" );

            GUILayout.Label( "When generate scene menu, set buildScenes automatically." );
            GUILayout.Label( "Drag and Drop the directory and the first scene." );
            GUILayout.Label( "Build Scene Dirrectory", EditorStyles.boldLabel );

            var curEvent = Event.current;
            var dropArea = EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField( m_buildScenePath );
            EditorGUILayout.EndHorizontal();

            GUILayout.Space( 5f );

            GUILayout.Label( "First Scene Path", EditorStyles.boldLabel );
            var dropArea2 = EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField( m_firstScenePath );
            EditorGUILayout.EndHorizontal();

            var buildScenePath = getDraggedAssetPath( curEvent, dropArea );
            var firstScenePath = getDraggedAssetPath( curEvent, dropArea2 );

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if ( GUILayout.Button( "clear", GUILayout.Width( 100f ) ) ) {
                GUIUtility.keyboardControl = 0;
                EditorPrefs.SetString( KEY_BUILDSCENEPATH, "" );
                EditorPrefs.SetString( KEY_FIRSTSCENEPATH, "" );
            }
            EditorGUILayout.EndHorizontal();

            if ( !string.IsNullOrEmpty( buildScenePath ) ) {
                m_buildScenePath = buildScenePath;
                EditorPrefs.SetString( KEY_BUILDSCENEPATH, m_buildScenePath );
            }
            if ( !string.IsNullOrEmpty( firstScenePath ) ) {
                m_firstScenePath = firstScenePath;
                EditorPrefs.SetString( KEY_FIRSTSCENEPATH, m_firstScenePath );
            }

        }

        //===========================================
        // Process
        //===========================================

        /// <summary>
        /// register build scene automatically
        /// </summary>
        private static void registerBuildScene() {
            try {

                m_buildScenePath = EditorPrefs.GetString( KEY_BUILDSCENEPATH, "" );
                m_firstScenePath = EditorPrefs.GetString( KEY_FIRSTSCENEPATH, "" );
                if ( string.IsNullOrEmpty( m_buildScenePath ) )
                    return;

                var sceneFilePathList = getSceneFilePath();
                if ( sceneFilePathList == null || sceneFilePathList.Count == 0 )
                    return;

                var buildScenes = new List<EditorBuildSettingsScene>();
                for ( int i = 0; i < sceneFilePathList.Count; i++ ) {
                    var path = sceneFilePathList[i];
                    if ( path.Contains( m_buildScenePath ) ) {
                        // check first scene
                        if ( !string.IsNullOrEmpty( m_firstScenePath ) ) {
                            if ( path.Equals( m_firstScenePath ) ) {
                                buildScenes.Insert( 0, new EditorBuildSettingsScene( path, true ) );
                            } else {
                                buildScenes.Add( new EditorBuildSettingsScene( path, true ) );
                            }
                        } else {
                            buildScenes.Add( new EditorBuildSettingsScene( path, true ) );
                        }

                    }
                }

                EditorBuildSettings.scenes = buildScenes.ToArray();

            } catch ( System.Exception e ) {
                Debug.LogError( e );
            }

        }

        /// <summary>
        /// Load scenes from project and return scene class list
        /// </summary>
        private static List<Scene> getScenesFromProject() {
            var sceneList = new List<Scene>();

            // get paths
            var sceneFilePathList = getSceneFilePath();
            if ( sceneFilePathList == null || sceneFilePathList.Count == 0 )
                return null;

            // check sceneName
            for ( int i = 0; i < sceneFilePathList.Count; i++ ) {
                var path          = sceneFilePathList[i];
                var sceneFileName = Path.GetFileNameWithoutExtension( path );

                if ( !Regex.IsMatch( sceneFileName, PATTERN_SCENENAME_INVALID ) ) {
                    // remove invalid character
                    var sceneName = sceneFileName.Replace( " ", "_" );
                    sceneName = sceneName.Replace( "(", "_" );
                    sceneName = sceneName.Replace( ")", "_" );
                    sceneName = sceneName.Replace( "-", "_" );
                    sceneName = sceneName.Replace( "&", "_" );

                    // check scene name duplicate
                    var duplicates = sceneList.Count( s => s.sceneName == sceneName );
                    if ( duplicates > 0 )
                        sceneName += "_" + duplicates.ToString();

                    // add list
                    sceneList.Add( new Scene( path, sceneName ) );
                } else {
                    Debug.LogError( "This Scene contains inValid character. " + sceneFileName );
                    continue;
                }
            }

            return sceneList;
        }

        /// <summary>
        /// Generate scene load method string from list and return method string
        /// </summary>
        private static string generateMethods( List<Scene> sceneList ) {
            StringBuilder m_methods = new StringBuilder( sceneList.Count );

            for ( int i = 0; i < sceneList.Count; i++ ) {
                m_methods.Append( generateMethod( sceneList[i], TEMPLATE_ADD, MENU_ADD, PRIORITY_ADD, "_Add" ) );
                m_methods.Append( generateMethod( sceneList[i], TEMPLATE_LOAD, MENU_NORMAL, PRIORITY_LOAD ) );
                if ( sceneList[i].isTest )
                    m_methods.Append( generateMethod( sceneList[i], TEMPLATE_LOAD, MENU_TEST, PRIORITY_LOAD, "_Test" ) );
                if ( sceneList[i].inBuild )
                    m_methods.Append( generateMethod( sceneList[i], TEMPLATE_LOAD, MENU_BUILD, PRIORITY_BUILD, "_InBuild" ) );
            }

            return m_methods.ToString();
        }

        /// <summary>
        /// generate a method from scene class
        /// </summary>
        private static string generateMethod( Scene scene, string methodTemplate, string menu, string priority, string type = "" ) {
            var m_method = methodTemplate;
            var m_loadMenu = menu.Replace( REPLACE_NAME, scene.sceneName );

            m_method = m_method.Replace( REPLACE_MENU, m_loadMenu );
            m_method = m_method.Replace( REPLACE_NAME, scene.sceneName + type );
            m_method = m_method.Replace( REPLACE_PATH, "\"" + scene.path + "\"" );
            m_method = m_method.Replace( REPLACE_PRIORITY, priority );

            return m_method;
        }

        /// <summary>
        /// save SceneLoadMenu.cs
        /// </summary>
        private static void saveSceneLoadMenuScript( string methods ) {
            if ( string.IsNullOrEmpty( methods ) ) {
                Debug.LogWarning("Error occured when generating SceneLoadMenu.cs");
                return;
            }
            // find save path
            var guids = AssetDatabase.FindAssets( SEARCH_TIHS_SCRIPT );
            var saveDir = Path.GetDirectoryName( AssetDatabase.GUIDToAssetPath( guids[0] ) );
            var savePath = Path.Combine( saveDir, SCENELOADMENU_FILENAME );

            // generate class
            var saveClass = TEMPLATE_CLASS.Replace( REPLACE_METHOD, methods );

            if ( File.Exists( savePath ) )
                Debug.Log( "Menu will be overwritten." );

            // save file
            using ( StreamWriter sw = new StreamWriter( savePath ) ) {
                sw.Write( saveClass );
                sw.Close();
            }

            AssetDatabase.Refresh();

            Debug.Log( "Menu is created." );
        }

        //===========================================
        // Util
        //===========================================

        private static List<string> getSceneFilePath() {
            var guids = AssetDatabase.FindAssets( SEARCH_SCENE_FILTER );
            var sceneNum = guids.Length;
            if ( sceneNum == 0 )
                return null;

            var sceneFileNameList = new List<string>();
            for ( int i = 0; i < sceneNum; i++ ) {
                var path = AssetDatabase.GUIDToAssetPath( guids[i] );

                sceneFileNameList.Add( path );
            }

            return sceneFileNameList;
        }

        /// <summary>
        /// get path of dropped file
        /// </summary>
        private static string getDraggedAssetPath( Event curEvent, Rect dropArea ) {
            int ctrlID = GUIUtility.GetControlID( FocusType.Passive );
            switch ( curEvent.type ) {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if ( !dropArea.Contains( curEvent.mousePosition ) )
                        break;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    DragAndDrop.activeControlID = ctrlID;

                    if ( curEvent.type == EventType.DragPerform ) {
                        DragAndDrop.AcceptDrag();
                        foreach ( var draggedObj in DragAndDrop.objectReferences ) {
                            return AssetDatabase.GetAssetPath( draggedObj );
                        }
                    }
                    break;
            }
            return null;
        }

        //===========================================
        // Class
        //===========================================

        class Scene {
            public string path;
            public string sceneName;
            public bool inBuild;
            public bool isTest;

            public Scene( string _path, string _scenename ) {
                path        = _path;
                sceneName   = _scenename;
                inBuild = EditorBuildSettings.scenes.Any( b => b.path == path );
                isTest = sceneName.ToLower().Contains( "test" ) || sceneName.ToLower().Contains( "debug" ) || path.ToLower().Contains( "example" ) || path.ToLower().Contains( "plugin" );
            }
        }

    }
}
