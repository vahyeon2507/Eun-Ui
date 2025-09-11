using UnityEngine;
using UnityEditor;

// custom editor for JangsanbeomBoss
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

    // the list we care most about
    SerializedProperty attacksProp;

    void OnEnable()
    {
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

        attacksProp = serializedObject.FindProperty("attacks");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Refs", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(playerProp);
        EditorGUILayout.PropertyField(animatorProp);
        EditorGUILayout.PropertyField(spriteRendererProp);
        EditorGUILayout.PropertyField(rbProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Prefabs & Visuals", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(telegraphPrefabProp);
        EditorGUILayout.PropertyField(hitboxPrefabProp);
        EditorGUILayout.PropertyField(decoyPrefabProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("AI / Movement", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(followSpeedProp);
        EditorGUILayout.PropertyField(followMinDistanceXProp);
        EditorGUILayout.PropertyField(aggroRangeProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Fake Claw", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(enableFakeClawProp);
        EditorGUILayout.PropertyField(fakeChanceProp);
        EditorGUILayout.PropertyField(fakeSpriteProp);
        EditorGUILayout.PropertyField(fakeSpriteDurationProp);
        EditorGUILayout.PropertyField(fakeOriginProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Hit Target", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(hitboxTargetLayerProp);
        EditorGUILayout.PropertyField(hitboxTargetTagProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animator Param Names", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(trigClawExecuteProp, new GUIContent("ClawExecute Trigger"));
        EditorGUILayout.PropertyField(trigClawFakeExecuteProp, new GUIContent("ClawFakeExecute Trigger"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Attacks (configurable)", EditorStyles.boldLabel);

        // draw the attacks list with foldout items
        if (attacksProp != null)
        {
            EditorGUILayout.PropertyField(attacksProp, new GUIContent("Attacks"), true);
        }
        else
        {
            EditorGUILayout.HelpBox("attacks property not found. Make sure JangsanbeomBoss has public List<AttackData> attacks.", MessageType.Warning);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
