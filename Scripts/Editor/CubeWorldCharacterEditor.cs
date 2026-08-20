using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CubeWorldCharacter))]
[CanEditMultipleObjects]
public class CubeWorldCharacterEditor : Editor
{
    private bool showVisualEditor = true;
    private SerializedProperty weaponLocalPos;
    private SerializedProperty weaponLocalRot;
    private SerializedProperty weaponLocalScale;

    void OnEnable()
    {
        weaponLocalPos = serializedObject.FindProperty("weaponLocalPosition");
        weaponLocalRot = serializedObject.FindProperty("weaponLocalRotation");
        weaponLocalScale = serializedObject.FindProperty("weaponLocalScale");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        CubeWorldCharacter character = (CubeWorldCharacter)target;

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("🎯 ВИЗУАЛЬНОЕ РЕДАКТИРЕНИЕ ОРУЖИЯ", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. Выбери инструмент 'Move Tool' (W)\n" +
            "2. В иерархии найди 'WeaponAnchor' (внутри правой руки)\n" +
            "3. Двигай/вращай его мышкой в сцене\n" +
            "4. Значения ниже обновятся автоматически\n" +
            "5. Ctrl+S — сохранить",
            MessageType.Info);

        showVisualEditor = EditorGUILayout.Foldout(showVisualEditor, "⚙️ Точная настройка положения оружия");
        if (showVisualEditor)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(weaponLocalPos, new GUIContent("📍 Позиция в руке (X/Y/Z)"));
            EditorGUILayout.PropertyField(weaponLocalRot, new GUIContent("🔄 Вращение (наклон/поворот/крен)"));
            EditorGUILayout.PropertyField(weaponLocalScale, new GUIContent("📏 Масштаб якоря"));
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔄 Сбросить в дефолт"))
            {
                character.ResetWeaponAnchorToDefault();
                EditorUtility.SetDirty(character);
            }
            if (GUILayout.Button("💾 Запомнить текущее"))
            {
                if (character.WeaponAnchor != null)
                {
                    weaponLocalPos.vector3Value = character.WeaponAnchor.localPosition;
                    weaponLocalRot.vector3Value = character.WeaponAnchor.localEulerAngles;
                    weaponLocalScale.vector3Value = character.WeaponAnchor.localScale;
                    serializedObject.ApplyModifiedProperties();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Остальные настройки персонажа:", EditorStyles.boldLabel);

        // Рисуем все остальные поля (кроме уже отрисованных вручную)
        DrawPropertiesExcluding(serializedObject,
            "m_Script",
            "weaponLocalPosition",
            "weaponLocalRotation",
            "weaponLocalScale"
        );

        if (serializedObject.ApplyModifiedProperties())
        {
            character.ApplyWeaponAnchorSettings();
            EditorUtility.SetDirty(character);
        }
    }

    // 🆕 Рисует кружок в сцене, куда нужно поставить автомат
    [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
    static void DrawWeaponAnchorGizmo(CubeWorldCharacter character, GizmoType gizmoType)
    {
        if (character.WeaponAnchor == null) return;

        // Кружок в точке якоря
        Handles.color = Color.yellow;
        Handles.DrawWireDisc(character.WeaponAnchor.position, character.WeaponAnchor.forward, 0.08f);

        // Стрелка направления
        Handles.color = Color.red;
        Handles.ArrowHandleCap(0,
            character.WeaponAnchor.position,
            character.WeaponAnchor.rotation,
            0.4f,
            EventType.Repaint);

        // Подпись
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.yellow;
        style.fontSize = 14;
        style.fontStyle = FontStyle.Bold;
        Handles.Label(
            character.WeaponAnchor.position + Vector3.up * 0.15f,
            "🎯 WeaponAnchor"
        );
    }
}