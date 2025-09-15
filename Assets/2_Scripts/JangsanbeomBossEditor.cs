using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(JangsanbeomBoss))]
public class JangsanbeomBossEditor : Editor
{
    SerializedProperty playerProp;
    SerializedProperty animatorProp;
    SerializedProperty spriteRendererProp;
    SerializedProperty rbProp;

    SerializedProperty telegraphPrefabProp;
    SerializedProperty hitboxPrefabProp;
    SerializedProperty decoyPrefabProp;

    SerializedProperty followSpeedProp;
    SerializedProperty followMinDistanceXProp;
    SerializedProperty aggroRangeProp;

    SerializedProperty enableFakeClawProp;
    SerializedProperty fakeChanceProp;
    SerializedProperty fakeSpriteProp;
    SerializedProperty fakeSpriteDurationProp;
    SerializedProperty fakeOriginProp;

    SerializedProperty hitboxTargetLayerProp;
    SerializedProperty hitboxTargetTagProp;

    SerializedProperty trigClawExecuteProp;
    SerializedProperty trigClawFakeExecuteProp;
    SerializedProperty trigClawTelegraphProp;
    SerializedProperty trigClawFakeTelegraphProp;

    SerializedProperty animFacingParamProp;

    // the list we care most about
    SerializedProperty attacksProp;

    void OnEnable()
    {
        // 안전하게 FindProperty 시도 (프로퍼티가 없으면 null 허용)
        playerProp = serializedObject.FindProperty("player");
        animatorProp = serializedObject.FindProperty("animator");
        spriteRendererProp = serializedObject.FindProperty("spriteRenderer");
        rbProp = serializedObject.FindProperty("rb");

        telegraphPrefabProp = serializedObject.FindProperty("telegraphPrefab");
        hitboxPrefabProp = serializedObject.FindProperty("hitboxPrefab");
        decoyPrefabProp = serializedObject.FindProperty("decoyPrefab");

        followSpeedProp = serializedObject.FindProperty("followSpeed");
        followMinDistanceXProp = serializedObject.FindProperty("followMinDistanceX");
        aggroRangeProp = serializedObject.FindProperty("aggroRange");

        enableFakeClawProp = serializedObject.FindProperty("enableFakeClaw");
        fakeChanceProp = serializedObject.FindProperty("fakeChance");
        fakeSpriteProp = serializedObject.FindProperty("fakeSprite");
        fakeSpriteDurationProp = serializedObject.FindProperty("fakeSpriteDuration");
        fakeOriginProp = serializedObject.FindProperty("fakeOrigin");

        hitboxTargetLayerProp = serializedObject.FindProperty("hitboxTargetLayer");
        hitboxTargetTagProp = serializedObject.FindProperty("hitboxTargetTag");

        trigClawExecuteProp = serializedObject.FindProperty("trig_ClawExecute");
        trigClawFakeExecuteProp = serializedObject.FindProperty("trig_ClawFakeExecute");
        trigClawTelegraphProp = serializedObject.FindProperty("trig_ClawTelegraph");
        trigClawFakeTelegraphProp = serializedObject.FindProperty("trig_ClawFakeTelegraph");

        animFacingParamProp = serializedObject.FindProperty("animFacingParam");

        attacksProp = serializedObject.FindProperty("attacks");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Refs
        EditorGUILayout.LabelField("Refs", EditorStyles.boldLabel);
        SafePropertyField(playerProp);
        SafePropertyField(animatorProp);
        SafePropertyField(spriteRendererProp);
        SafePropertyField(rbProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Prefabs & Visuals", EditorStyles.boldLabel);
        SafePropertyField(telegraphPrefabProp);
        SafePropertyField(hitboxPrefabProp);
        SafePropertyField(decoyPrefabProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("AI / Movement", EditorStyles.boldLabel);
        SafePropertyField(followSpeedProp);
        SafePropertyField(followMinDistanceXProp);
        SafePropertyField(aggroRangeProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Fake Claw", EditorStyles.boldLabel);
        SafePropertyField(enableFakeClawProp);
        SafePropertyField(fakeChanceProp);
        SafePropertyField(fakeSpriteProp);
        SafePropertyField(fakeSpriteDurationProp);
        SafePropertyField(fakeOriginProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Hit Target", EditorStyles.boldLabel);
        SafePropertyField(hitboxTargetLayerProp);
        SafePropertyField(hitboxTargetTagProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animator Param Names", EditorStyles.boldLabel);
        SafePropertyField(trigClawTelegraphProp, new GUIContent("ClawTelegraph Trigger"));
        SafePropertyField(trigClawExecuteProp, new GUIContent("ClawExecute Trigger"));
        SafePropertyField(trigClawFakeTelegraphProp, new GUIContent("ClawFakeTelegraph Trigger"));
        SafePropertyField(trigClawFakeExecuteProp, new GUIContent("ClawFakeExecute Trigger"));
        SafePropertyField(animFacingParamProp, new GUIContent("Facing param (animator)"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Attacks (configurable)", EditorStyles.boldLabel);

        if (attacksProp != null)
        {
            // draw the property with foldout + children
            EditorGUILayout.PropertyField(attacksProp, new GUIContent("Attacks"), true);

            // small help text
            EditorGUILayout.HelpBox("Each Attack entry (AttackData) can define Offset/Size/Damage/etc. Use Expand (triangle) to edit.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("attacks property not found. Make sure JangsanbeomBoss has a public List<AttackData> attacks field and AttackData is [System.Serializable].", MessageType.Warning);
        }

        // show runtime debug values and quick actions when in Play mode
        if (Application.isPlaying)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime (Play Mode)", EditorStyles.boldLabel);
            JangsanbeomBoss boss = (JangsanbeomBoss)target;
            if (boss != null)
            {
                EditorGUILayout.LabelField("busy", boss.busy.ToString());
                EditorGUILayout.LabelField("facingRight", boss.facingRight.ToString());
                if (GUILayout.Button("Force flip (editor)")) { boss.FlipTo(!boss.facingRight); EditorUtility.SetDirty(boss); }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    // Helper: only draw property if not null
    void SafePropertyField(SerializedProperty prop, GUIContent label = null)
    {
        if (prop == null)
        {
            if (label != null) EditorGUILayout.LabelField(label.text, "<missing>");
            return;
        }
        if (label != null) EditorGUILayout.PropertyField(prop, label);
        else EditorGUILayout.PropertyField(prop);
    }
}
