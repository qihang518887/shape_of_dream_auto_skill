using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AutoUseSkill
{
    public class AutoUseSkill : ModBehaviour
    {
        private bool AutoCastOutOfCombat;
        private bool ShowGUI;
        private KeyCode OpenMenuKey = KeyCode.I;
        private float ButtonHeight = 0.035f;
        private Rect WindowRect;
        private bool Auto_Attack;
        private bool[] AutoUseDict = new bool[4];
        private double lastSearchTime = 0d;
        private double SearchInterval = 0.01d;

        private string feedbackText = "";
        private float feedbackEndTime = 0f;

        private static bool IsInCombatRoom
        {
            get
            {
                try
                {
                    string sceneName = SceneManager.GetActiveScene().name;
                    return sceneName != null && sceneName.StartsWith("Room_");
                }
                catch
                {
                    return false;
                }
            }
        }

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

            // Zero overhead when outside actual combat rooms (in Lobby, Traveler settings, Title, etc.)
            if (!IsInCombatRoom) return;

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
            if (Input.GetKeyDown(KeyCode.F5))
            {
                Auto_Attack = !Auto_Attack;
                ShowFeedback("自动普通攻击: " + (Auto_Attack ? "开启 (ON)" : "关闭 (OFF)"));
            }

            if (Player == null || (!AutoCastOutOfCombat && !Player.isInCombat)) return;
            if (Time.time - lastSearchTime < SearchInterval) return;
            lastSearchTime = Time.time;
            TryAutoCastSkill();
            TryAutoAttack();
        }

        private Entity FindClosestEnemy(float maxRange)
        {
            if (Player == null) return null;
            Entity closest = null;
            float minDistSq = maxRange * maxRange;
            Vector3 playerPos = Player.agentPosition;

            try
            {
                if (NetworkedManagerBase<ActorManager>.instance != null && NetworkedManagerBase<ActorManager>.instance.allEntities != null)
                {
                    foreach (Entity e in NetworkedManagerBase<ActorManager>.instance.allEntities)
                    {
                        if (e == null || !e.isActive) continue;
                        if (Player.GetRelation(e) != EntityRelation.Enemy) continue;
                        if (e.Status != null && e.Status.isUndetectableByNonAllies) continue;

                        float dSq = (e.agentPosition - playerPos).sqrMagnitude;
                        if (dSq < minDistSq)
                        {
                            minDistSq = dSq;
                            closest = e;
                        }
                    }
                }
            }
            catch { }

            return closest;
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

                float range = skill.currentConfig != null ? skill.currentConfig.effectiveRange : 8f;
                Entity target = controlManager.targetEnemy;
                if (target == null || !target.isActive || Player.GetRelation(target) != EntityRelation.Enemy)
                {
                    target = FindClosestEnemy(range);
                }

                if (target != null && skill.currentConfig != null && !skill.currentConfig.CheckRange(Player, target))
                {
                    continue;
                }

                if (target != null)
                {
                    controlManager.CastAbility(skill, new CastInfo(Player, target), false);
                }
                else
                {
                    controlManager.CastAbilityAuto(skill);
                }
            }
        }

        private void TryAutoAttack()
        {
            if (!Auto_Attack || Player == null) return;
            AbilityTrigger attackAbility = Player.Ability.attackAbility;
            if (attackAbility == null || attackAbility.IsNullOrInactive()) return;
            if (!attackAbility.CanBeCast()) return;

            float range = attackAbility.currentConfig != null ? attackAbility.currentConfig.effectiveRange : 4.5f;
            if (range < 2.5f) range = 2.5f;

            // 1. Native attack-move target finder
            Entity target = null;
            try
            {
                target = ActionAttackMove.FindAttackMoveTarget(Player, Player.agentPosition);
            }
            catch { }

            // 2. Fallback to closest enemy within range
            if (target == null || !target.isActive || Player.GetRelation(target) != EntityRelation.Enemy)
            {
                target = FindClosestEnemy(range);
            }

            if (target == null || !target.isActive) return;

            // 3. Clear movement and issue server attack command
            Player.Control.CmdClearMovement();
            Player.Control.CmdAttack(target, true);

            // 4. Also trigger immediate attack cast on server
            try
            {
                Player.Control.CmdCast(attackAbility, attackAbility.currentConfigIndex, new CastInfo(Player, target), true, false);
            }
            catch { }
        }

        private void DrawSkillBadges()
        {
            try
            {
                if (ManagerBase<UI_InGame_SkillButtons>.instance == null || ManagerBase<UI_InGame_SkillButtons>.instance.skillButtons == null)
                    return;

                UI_InGame_SkillButton[] skillBtns = ManagerBase<UI_InGame_SkillButtons>.instance.skillButtons;
                Vector3 firstPos = Vector3.zero;
                float firstHalfH = 28f;
                bool foundFirst = false;

                for (int i = 0; i < skillBtns.Length; i++)
                {
                    UI_InGame_SkillButton btn = skillBtns[i];
                    if (btn == null || !btn.isActiveAndEnabled) continue;

                    int slot = -1;
                    switch (btn.skillType)
                    {
                        case HeroSkillLocation.Q: slot = 0; break;
                        case HeroSkillLocation.W: slot = 1; break;
                        case HeroSkillLocation.E: slot = 2; break;
                        case HeroSkillLocation.R: slot = 3; break;
                    }

                    if (slot >= 0 && slot < 4)
                    {
                        Vector3 pos = btn.icon != null ? btn.icon.transform.position : btn.transform.position;
                        float cx = pos.x;
                        float cy = Screen.height - pos.y;
                        float halfH = 28f;

                        if (btn.icon != null && btn.icon.rectTransform != null)
                        {
                            halfH = btn.icon.rectTransform.rect.height * 0.5f * btn.icon.transform.lossyScale.y;
                            if (halfH <= 0f) halfH = 28f;
                        }

                        if (!foundFirst)
                        {
                            firstPos = pos;
                            firstHalfH = halfH;
                            foundFirst = true;
                        }

                        // Badge positioned neatly inside the skill icon near the top
                        Rect badgeRect = new Rect(cx - 17f, cy - halfH + 4f, 34f, 16f);
                        bool isSkillOn = AutoUseDict[slot];
                        Color oldCol = GUI.color;
                        GUI.color = isSkillOn ? new Color(0.1f, 1f, 0.3f, 0.95f) : new Color(1f, 0.25f, 0.25f, 0.95f);
                        GUI.Box(badgeRect, isSkillOn ? "ON" : "OFF");
                        GUI.color = oldCol;
                    }
                }

                if (foundFirst)
                {
                    // Draw Auto Attack badge to the left of Q skill button
                    float qx = firstPos.x;
                    float qy = Screen.height - firstPos.y;
                    Rect atkRect = new Rect(qx - 66f, qy - firstHalfH + 4f, 58f, 18f);
                    Color oldCol = GUI.color;
                    GUI.color = Auto_Attack ? new Color(0.1f, 1f, 0.3f, 0.95f) : new Color(1f, 0.25f, 0.25f, 0.95f);
                    GUI.Box(atkRect, Auto_Attack ? "普攻 ON" : "普攻 OFF");
                    GUI.color = oldCol;
                }
            }
            catch
            {
                // Never throw or stall inside OnGUI
            }
        }

        private void OnGUI()
        {
            // If in Lobby, Traveler Settings, Constellations, or Main Menu: NEVER run badge checks or layout!
            if (!IsInCombatRoom)
            {
                if (ShowGUI)
                {
                    WindowRect = GUILayout.Window(9999, WindowRect, MenuGui, "Auto Use Skill", "box");
                }
                return;
            }

            // 1. Toast floating feedback banner (1.5 seconds)
            if (Time.time < feedbackEndTime && !string.IsNullOrEmpty(feedbackText))
            {
                GUI.Box(new Rect(Screen.width * 0.38f, 15f, Screen.width * 0.24f, 32f), feedbackText);
            }

            // 2. Persistent ON/OFF badges directly on in-game skill icons (only during Repaint)
            if (Event.current.type == EventType.Repaint)
            {
                DrawSkillBadges();
            }

            // 3. Settings window
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
            AutoCastOutOfCombat = GUILayout.Toggle(AutoCastOutOfCombat, "脱战施法 (Out Of Combat)", option);
            Auto_Attack = GUILayout.Toggle(Auto_Attack, "自动普攻 (F5)", option);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
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