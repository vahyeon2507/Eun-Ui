#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CameraSimple2D))]
public class CameraSimple2DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 기본 인스펙터 먼저
        DrawDefaultInspector();

        var cam = (CameraSimple2D)target;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("— Test Actions —", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Test Shake"))
            {
                cam.Editor_TestShake();
                // 플레이 모드가 아니면 씬 변동만 표시
                if (!Application.isPlaying) EditorUtility.SetDirty(cam);
            }

            if (GUILayout.Button("Test Punch Zoom"))
            {
                // 펀치줌은 코루틴이라 Play모드에서만 실동작
                if (Application.isPlaying) cam.Editor_TestPunchZoom();
                else Debug.LogWarning("PunchZoom은 Play 모드에서만 동작합니다.");
                EditorUtility.SetDirty(cam);
            }
        }
    }
}
#endif
