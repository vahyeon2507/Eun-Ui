//#if UNITY_EDITOR
//using UnityEngine;
//using UnityEditor;

//[CustomEditor(typeof(JangsanbeomBoss)), CanEditMultipleObjects]
//public class JangsanbeomBossEditor : Editor
//{
//    // Refs
//    SerializedProperty playerProp, animatorProp, spriteRendererProp, rbProp;

//    // Prefabs & visuals
//    SerializedProperty telegraphPrefabProp, hitboxPrefabProp, decoyPrefabProp;

//    // AI / Movement
//    SerializedProperty followSpeedProp, followMinDistanceXProp, aggroRangeProp;

//    // Default claw
//    SerializedProperty defaultClawOffsetXProp, defaultClawSizeProp, defaultClawDamageProp, defaultHitboxLifetimeProp;

//    // Fake claw
//    SerializedProperty enableFakeClawProp, fakeChanceProp, fakeSpriteProp, fakeSpriteDurationProp, fakeOriginProp;

//    // Hit target
//    SerializedProperty hitboxTargetLayerProp, hitboxTargetTagProp;

//    // Animator params
//    SerializedProperty trigClawTelegraphProp, trigClawExecuteProp, trigClawFakeTelegraphProp, trigClawFakeExecuteProp;

//    // Behavior options
//    SerializedProperty useAnimationEventsProp;

//    // Flip pivot / deadzone / cooldown + debug
//    SerializedProperty flipPivotProp, flipDeadzoneProp, flipCooldownProp, debugPolygonFlipProp;

//    // Gizmos
//    SerializedProperty showGizmosProp, gizmoRealColorProp, gizmoFakeColorProp;

//    // Attacks
//    SerializedProperty attacksProp;

//    void OnEnable()
//    {
//        var so = serializedObject;

//        // Refs
//        playerProp = so.FindProperty("player");
//        animatorProp = so.FindProperty("animator");
//        spriteRendererProp = so.FindProperty("spriteRenderer");
//        rbProp = so.FindProperty("rb");

//        // Prefabs & visuals
//        telegraphPrefabProp = so.FindProperty("telegraphPrefab");
//        hitboxPrefabProp = so.FindProperty("hitboxPrefab");
//        decoyPrefabProp = so.FindProperty("decoyPrefab");

//        // AI / Movement
//        followSpeedProp = so.FindProperty("followSpeed");
//        followMinDistanceXProp = so.FindProperty("followMinDistanceX");
//        aggroRangeProp = so.FindProperty("aggroRange");

//        // Default claw
//        defaultClawOffsetXProp = so.FindProperty("defaultClawOffsetX");
//        defaultClawSizeProp = so.FindProperty("defaultClawSize");
//        defaultClawDamageProp = so.FindProperty("defaultClawDamage");
//        defaultHitboxLifetimeProp = so.FindProperty("defaultHitboxLifetime");

//        // Fake claw
//        enableFakeClawProp = so.FindProperty("enableFakeClaw");
//        fakeChanceProp = so.FindProperty("fakeChance");
//        fakeSpriteProp = so.FindProperty("fakeSprite");
//        fakeSpriteDurationProp = so.FindProperty("fakeSpriteDuration");
//        fakeOriginProp = so.FindProperty("fakeOrigin");

//        // Hit target
//        hitboxTargetLayerProp = so.FindProperty("hitboxTargetLayer");
//        hitboxTargetTagProp = so.FindProperty("hitboxTargetTag");

//        // Animator params
//        trigClawTelegraphProp = so.FindProperty("trig_ClawTelegraph");
//        trigClawExecuteProp = so.FindProperty("trig_ClawExecute");
//        trigClawFakeTelegraphProp = so.FindProperty("trig_ClawFakeTelegraph");
//        trigClawFakeExecuteProp = so.FindProperty("trig_ClawFakeExecute");

//        // Behavior options
//        useAnimationEventsProp = so.FindProperty("useAnimationEvents");

//        // Flip pivot / deadzone / cooldown + debug
//        flipPivotProp = so.FindProperty("flipPivot");
//        flipDeadzoneProp = so.FindProperty("flipDeadzone");
//        flipCooldownProp = so.FindProperty("flipCooldown");
//        debugPolygonFlipProp = so.FindProperty("debugPolygonFlip");

//        // Gizmos
//        showGizmosProp = so.FindProperty("showGizmos");
//        gizmoRealColorProp = so.FindProperty("gizmoRealColor");
//        gizmoFakeColorProp = so.FindProperty("gizmoFakeColor");

//        // Attacks
//        attacksProp = so.FindProperty("attacks");
//    }

//    public override void OnInspectorGUI()
//    {
//        serializedObject.Update();

//        DrawHeader("Refs");
//        Field(playerProp);
//        Field(animatorProp);
//        Field(spriteRendererProp);
//        Field(rbProp);

//        DrawHeader("Prefabs & Visuals");
//        Field(telegraphPrefabProp);
//        Field(hitboxPrefabProp);
//        Field(decoyPrefabProp);

//        DrawHeader("AI / Movement");
//        Field(followSpeedProp);
//        Field(followMinDistanceXProp, new GUIContent("Follow Min Distance X"));
//        Field(aggroRangeProp);

//        DrawHeader("Default Claw Settings");
//        Field(defaultClawOffsetXProp, new GUIContent("Default Claw Offset X"));
//        Field(defaultClawSizeProp, new GUIContent("Default Claw Size"));
//        Field(defaultClawDamageProp, new GUIContent("Default Claw Damage"));
//        Field(defaultHitboxLifetimeProp, new GUIContent("Default Hitbox Lifetime"));

//        DrawHeader("Fake Claw");
//        Field(enableFakeClawProp);
//        Field(fakeChanceProp);
//        Field(fakeSpriteProp);
//        Field(fakeSpriteDurationProp);
//        Field(fakeOriginProp);

//        DrawHeader("Hit Target");
//        Field(hitboxTargetLayerProp, new GUIContent("Hitbox Target Layer"));
//        Field(hitboxTargetTagProp, new GUIContent("Hitbox Target Tag"));

//        DrawHeader("Animator Parameter Names");
//        Field(trigClawTelegraphProp, new GUIContent("ClawTelegraph Trigger"));
//        Field(trigClawExecuteProp, new GUIContent("ClawExecute Trigger"));
//        Field(trigClawFakeTelegraphProp, new GUIContent("ClawFakeTelegraph Trigger"));
//        Field(trigClawFakeExecuteProp, new GUIContent("ClawFakeExecute Trigger"));

//        DrawHeader("Behavior Options");
//        Field(useAnimationEventsProp);

//        DrawHeader("Flip Pivot / Deadzone");
//        Field(flipPivotProp, new GUIContent("Flip Pivot (Transform)"));
//        Field(flipDeadzoneProp, new GUIContent("Flip Deadzone (X units)"));
//        Field(flipCooldownProp, new GUIContent("Flip Cooldown (sec)"));
//        Field(debugPolygonFlipProp, new GUIContent("Debug Polygon Flip Logs"));

//        DrawHeader("Gizmos");
//        Field(showGizmosProp);
//        Field(gizmoRealColorProp);
//        Field(gizmoFakeColorProp);

//        DrawHeader("Attacks (configurable)");
//        if (attacksProp != null)
//        {
//            EditorGUILayout.PropertyField(attacksProp, new GUIContent("Attacks"), true);
//            EditorGUILayout.HelpBox("각 AttackData에서 Offset/Size/Damage/Angle/Lifetime 등을 설정.", MessageType.Info);
//        }
//        else
//        {
//            EditorGUILayout.HelpBox("attacks 필드를 찾지 못했습니다. 스크립트에 public List<AttackData> attacks 가 있는지 확인하세요.", MessageType.Warning);
//        }

//        // ---- Play-mode utilities ----
//        if (Application.isPlaying)
//        {
//            EditorGUILayout.Space();
//            EditorGUILayout.LabelField("Runtime (Play Mode)", EditorStyles.boldLabel);

//            var boss = (JangsanbeomBoss)target;
//            using (new EditorGUI.DisabledScope(true))
//            {
//                EditorGUILayout.Toggle("facingRight", boss.facingRight);
//                EditorGUILayout.Toggle("busy", boss.busy);
//            }

//            EditorGUILayout.BeginHorizontal();
//            if (GUILayout.Button("Face Left")) { boss.ForceFlip(false); EditorUtility.SetDirty(boss); }
//            if (GUILayout.Button("Face Right")) { boss.ForceFlip(true); EditorUtility.SetDirty(boss); }
//            EditorGUILayout.EndHorizontal();

//            if (GUILayout.Button("Test Attack[0] (real)")) { boss.StartAttackByIndex(0, true); }
//            if (GUILayout.Button("Test Attack[0] (fake)")) { boss.StartAttackByIndex(0, false); }
//        }

//        serializedObject.ApplyModifiedProperties();
//    }

//    // --------- helpers ----------
//    void DrawHeader(string title)
//    {
//        EditorGUILayout.Space();
//        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
//    }

//    void Field(SerializedProperty p, GUIContent label = null)
//    {
//        if (p == null)
//        {
//            if (label != null) EditorGUILayout.LabelField(label, new GUIContent("<missing>"));
//            return;
//        }
//        if (label != null) EditorGUILayout.PropertyField(p, label);
//        else EditorGUILayout.PropertyField(p);
//    }
//}
//#endif
