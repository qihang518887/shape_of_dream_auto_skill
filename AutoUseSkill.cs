using System;
using System.Collections.Generic;
using UnityEngine;

namespace AutoUseSkill
{
    public class AutoUseSkill : ModBehaviour
    {
        private bool ModEnabled = true;
        private bool AutoCastOutOfCombat;
        private bool ShowGUI;
        private KeyCode EnableKey = KeyCode.O;
        private KeyCode OpenMenuKey = KeyCode.I;
        private float ButtonHeight = 0.035f;
        private Rect WindowRect;
        private bool Auto_Attack;
        private bool[] AutoUseDict = new bool[4];
        private double lastSearchTime = 0d;
        private double SearchInterval = 0.01d;

        private string feedbackText = "";
        private float feedbackEndTime = 0f;

        private static Hero Player
        {
            get
            {
                if (ManagerBase<ControlManager>.instance != null)
                {
                    return ManagerBase<ControlManager>.instance.controllingEntity as Hero;
                }
                return null;
            }
        }

        private static ControlManager controlManager
        {
            get { return ManagerBase<ControlManager>.instance; }
        }

        private void Awake()
        {
            Debug.Log("[AutoUseSkill] Mod loaded successfully!");
            WindowRect = new Rect(Screen.width * 0.4f, Screen.height * 0.4f, Screen.width * 0.25f, Screen.height * ButtonHeight * 4.5f);
        }

        private void OnDestroy()
        {
            ShowGUI = false;
            ModEnabled = false;
            Debug.Log("[AutoUseSkill] Mod unloaded.");
        }

        private void ShowFeedback(string text)
        {
            feedbackText = text;
            feedbackEndTime = Time.time + 1.5f;
        }

        private void Update()
        {
            if (Input.GetKeyDown(OpenMenuKey))
                ShowGUI = !ShowGUI;

            if (Input.GetKeyDown(EnableKey))
            {
                ModEnabled = !ModEnabled;
                ShowFeedback("自动施法总开关: " + (ModEnabled ? "开启 (ON)" : "关闭 (OFF)"));
            }

            if (Input.GetKeyDown(KeyCode.F1))
            {
                AutoUseDict[0] = !AutoUseDict[0];
                ShowFeedback("Q 技能自动释放: " + (AutoUseDict[0] ? "开启 (ON)" : "关闭 (OFF)"));
            }
            if (Input.GetKeyDown(KeyCode.F2))
            {
                AutoUseDict[1] = !AutoUseDict[1];
                ShowFeedback("W 技能自动释放: " + (AutoUseDict[1] ? "开启 (ON)" : "关闭 (OFF)"));
            }
            if (Input.GetKeyDown(KeyCode.F3))
            {
                AutoUseDict[2] = !AutoUseDict[2];
                ShowFeedback("E 技能自动释放: " + (AutoUseDict[2] ? "开启 (ON)" : "关闭 (OFF)"));
            }
            if (Input.GetKeyDown(KeyCode.F4))
            {
                AutoUseDict[3] = !AutoUseDict[3];
                ShowFeedback("R 技能自动释放: " + (AutoUseDict[3] ? "开启 (ON)" : "关闭 (OFF)"));
            }

            if (!ModEnabled || Player == null || (!AutoCastOutOfCombat && !Player.isInCombat)) return;
            if (Time.time - lastSearchTime < SearchInterval) return;
            lastSearchTime = Time.time;
            TryAutoCastSkill();
            TryAutoAttack();
        }

        private void TryAutoCastSkill()
        {
            for (var i = 0; i <= 3; i++)
            {
                if (!AutoUseDict[i]) continue;
                AbilityTrigger abilityTrigger;
                if (!Player.Ability.abilities.TryGetValue(i, out abilityTrigger)) continue;
                SkillTrigger skill = abilityTrigger as SkillTrigger;
                if (skill == null) continue;
                if (!skill.CanBeCast()) continue;
                var shouldCast = true;
                switch (skill.currentConfig.castMethod.type)
                {
                    case CastMethodType.Cone:
                    case CastMethodType.Arrow:
                        if (controlManager.targetEnemy == null || !skill.currentConfig.CheckRange(Player, controlManager.targetEnemy)) shouldCast = false;
                        break;
                    case CastMethodType.Target:
                        if ((skill.currentConfig.targetValidator.targets & EntityRelation.Self) == 0)
                        {
                            if (controlManager.targetEnemy == null || !skill.currentConfig.CheckRange(Player, controlManager.targetEnemy)) shouldCast = false;
                        }
                        break;
                    case CastMethodType.None:
                    case CastMethodType.Point:
                    default:
                        break;
                }

                if (shouldCast) controlManager.CastAbilityAuto(skill);
            }
        }

        private void TryAutoAttack()
        {
            if (!Auto_Attack) return;
            AttackTrigger attackAbility = Player.Ability.attackAbility as AttackTrigger;
            if (attackAbility == null || attackAbility.IsNullOrInactive()) return;
            if (controlManager.targetEnemy == null || !attackAbility.currentConfig.CheckRange(Player, controlManager.targetEnemy)) return;
            Player.Control.CmdAttack(controlManager.targetEnemy, false);
        }

        private void OnGUI()
        {
            if (Time.time < feedbackEndTime && !string.IsNullOrEmpty(feedbackText))
            {
                GUI.Box(new Rect(Screen.width * 0.38f, 15f, Screen.width * 0.24f, 32f), feedbackText);
            }

            if (!ShowGUI) return;
            WindowRect = GUILayout.Window(9999, WindowRect, MenuGui, "Auto Use Skill", "box");
        }

        private void MenuGui(int id)
        {
            GUI.DragWindow(new Rect(0, 0, 1000, Screen.height * ButtonHeight));
            var option = GUILayout.Height(Screen.height * ButtonHeight);
            GUILayout.BeginVertical();
            GUILayout.Label("", option);
            GUILayout.BeginHorizontal();
            ModEnabled = GUILayout.Toggle(ModEnabled, "启用 (Enable)", option);
            AutoCastOutOfCombat = GUILayout.Toggle(AutoCastOutOfCombat, "脱战施法 (Out Of Combat)", option);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            Auto_Attack = GUILayout.Toggle(Auto_Attack, "自动普攻 (Attack)", option);
            AutoUseDict[0] = GUILayout.Toggle(AutoUseDict[0], "Q (F1)", option);
            AutoUseDict[1] = GUILayout.Toggle(AutoUseDict[1], "W (F2)", option);
            AutoUseDict[2] = GUILayout.Toggle(AutoUseDict[2], "E (F3)", option);
            AutoUseDict[3] = GUILayout.Toggle(AutoUseDict[3], "R (F4)", option);
            GUILayout.EndHorizontal();
            GUILayout.Label("检测间隔 (Interval): " + SearchInterval.ToString("0.00") + "s", option);
            SearchInterval = Math.Round(GUILayout.HorizontalSlider((float)SearchInterval, 0.01f, 0.5f, option), 2);
            GUILayout.EndVertical();
        }

        [ConsoleCommand("autoskill", "切换自动施法GUI显示")]
        public void CmdToggleGui()
        {
            ShowGUI = !ShowGUI;
        }
    }
}