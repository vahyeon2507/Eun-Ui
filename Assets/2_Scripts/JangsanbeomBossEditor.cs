// File: Assets/Editor/JangsanbeomBossEditor.cs
// MUST be placed in an Editor folder (e.g. Assets/Editor/)
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(JangsanbeomBoss))]
public class JangsanbeomBossEditor : Editor
{
    SerializedProperty attacksProp;
    SerializedProperty drawGizmosProp;
    JangsanbeomBoss boss;

    void OnEnable()
    {
        boss = (JangsanbeomBoss)target;
        attacksProp = serializedObject.FindProperty("attacks");
        drawGizmosProp = serializedObject.FindProperty("drawGizmos");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("player"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("animator"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("spriteRenderer"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rb"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("hitboxPrefab"));

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("followSpeed"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("followMinDistanceX"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("aggroRange"));

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(attacksProp, new GUIContent("Attacks"), true); // show array with foldouts

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("hitboxTargetLayer"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("hitboxTargetTag"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("useAnimationEvents"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cooldownTime"));
        EditorGUILayout.PropertyField(drawGizmosProp);

        EditorGUILayout.Space();
        if (GUILayout.Button("Add Attack"))
        {
            attacksProp.arraySize++;
            var el = attacksProp.GetArrayElementAtIndex(attacksProp.arraySize - 1);
            el.FindPropertyRelative("name").stringValue = "Attack " + (attacksProp.arraySize - 1);
            el.FindPropertyRelative("offset").vector2Value = new Vector2(1.5f, 0f);
            el.FindPropertyRelative("size").vector2Value = new Vector2(3f, 1.5f);
            el.FindPropertyRelative("damage").intValue = 2;
            serializedObject.ApplyModifiedProperties();
        }
        if (GUILayout.Button("Remove Last Attack"))
        {
            if (attacksProp.arraySize > 0) attacksProp.arraySize--;
        }

        serializedObject.ApplyModifiedProperties();
    }

    // Scene GUI: draw and allow handle editing for each attack
    void OnSceneGUI()
    {
        if (boss == null || boss.attacks == null) return;

        Transform t = boss.transform;
        for (int i = 0; i < boss.attacks.Length; i++)
        {
            AttackData a = boss.attacks[i];
            // compute world center
            Vector3 worldCenter = t.position + new Vector3(a.offset.x * (boss.transform.localScale.x >= 0 ? (boss.facingRight ? 1f : -1f) : 1f), a.offset.y, 0f);
            // Note: facingRight is private; use sign of localScale or handle flipping visually. We'll compute dir as (sprite flip considered)
            float dir = boss.transform.localScale.x >= 0 ? (boss.transform.localScale.x > 0 ? 1f : -1f) : 1f;
            dir = boss.transform.localScale.x > 0 ? (boss.facingRight ? 1f : -1f) : (boss.facingRight ? 1f : -1f);

            // Draw wire box
            Handles.color = Color.Lerp(Color.red, Color.yellow, i / (float)Mathf.Max(1, boss.attacks.Length - 1));
            // Use rotation
            Quaternion rot = Quaternion.Euler(0f, 0f, a.angle);
            Matrix4x4 prev = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(worldCenter, rot, Vector3.one);

            // Draw the box outline
            Vector3 size = new Vector3(a.size.x, a.size.y, 0.01f);
            Handles.DrawWireCube(Vector3.zero, size);

            Handles.matrix = prev;

            // Position handle (move offset)
            EditorGUI.BeginChangeCheck();
            var fmh_94_71_638929780432378229 = Quaternion.identity; Vector3 newWorldPos = Handles.FreeMoveHandle(worldCenter, 0.1f, Vector3.zero, Handles.CircleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(boss, "Move Attack Offset");
                Vector3 local = boss.transform.InverseTransformPoint(newWorldPos);
                // adjust x by facing sign
                float sign = boss.facingRight ? 1f : -1f;
                a.offset = new Vector2(local.x * sign, local.y);
                EditorUtility.SetDirty(boss);
            }

            // Size handles (two drag handles to change width & height)
            EditorGUI.BeginChangeCheck();
            // right handle
            Vector3 rightHandleWorld = worldCenter + rot * new Vector3(a.size.x * 0.5f, 0f, 0f);
            var fmh_109_73_638929780432395897 = Quaternion.identity; Vector3 newRight = Handles.FreeMoveHandle(rightHandleWorld, 0.08f, Vector3.zero, Handles.RectangleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(boss, "Resize Attack Width");
                // compute new width based on moved handle distance to center in local space
                Vector3 localRight = Quaternion.Inverse(rot) * (newRight - worldCenter);
                a.size = new Vector2(Mathf.Abs(localRight.x) * 2f, a.size.y);
                EditorUtility.SetDirty(boss);
            }

            EditorGUI.BeginChangeCheck();
            // top handle
            Vector3 topHandleWorld = worldCenter + rot * new Vector3(0f, a.size.y * 0.5f, 0f);
            var fmh_122_69_638929780432401791 = Quaternion.identity; Vector3 newTop = Handles.FreeMoveHandle(topHandleWorld, 0.08f, Vector3.zero, Handles.RectangleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(boss, "Resize Attack Height");
                Vector3 localTop = Quaternion.Inverse(rot) * (newTop - worldCenter);
                a.size = new Vector2(a.size.x, Mathf.Abs(localTop.y) * 2f);
                EditorUtility.SetDirty(boss);
            }

            // label with index/name
            Handles.Label(worldCenter + Vector3.up * (a.size.y * 0.5f + 0.15f), $"{a.name} ({i})");

            // small instruction: show attack index for animation events
            Handles.Label(worldCenter + Vector3.down * (a.size.y * 0.5f + 0.15f), $"Index: {i}");

        } // end for
    }
}
