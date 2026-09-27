using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AutoUseSkill
{
    public class AutoUseSkill : ModBehaviour
    {
        private const int WindowId = 894270;

        private bool AutoCastOutOfCombat;
        private bool ShowGUI;
        private KeyCode OpenMenuKey = KeyCode.I;
        private float ButtonHeight = 0.035f;
        private Rect WindowRect;
        private bool Auto_Attack;
        private bool[] AutoUseDict = new bool[4];
        private float lastSearchTime = 0f;
        private float SearchInterval = 0.05f;

        private string feedbackText = "";
        private float feedbackEndTime = 0f;

        private bool isInCombatRoom = false;
        private Canvas cachedCanvas = null;

        private bool IsInCombatRoom
        {
            get { return isInCombatRoom; }
        }

        private Hero Player
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

        private ControlManager controlManager
        {
            get { return ManagerBase<ControlManager>.instance; }
        }

        private void Awake()
        {
            Debug.Log("[AutoUseSkill] Mod loaded successfully!");
            // Dynamic window height calculated by GUILayout
            WindowRect = new Rect(Screen.width * 0.4f, Screen.height * 0.4f, Screen.width * 0.25f, 0f);
            UpdateCombatRoomStatus(SceneManager.GetActiveScene());
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            ShowGUI = false;
            Auto_Attack = false;
            if (AutoUseDict != null)
            {
                for (int i = 0; i < AutoUseDict.Length; i++)
                {
                    AutoUseDict[i] = false;
                }
            }
            cachedCanvas = null;
            Debug.Log("[AutoUseSkill] Mod unloaded.");
        }

        private void OnActiveSceneChanged(Scene current, Scene next)
        {
            cachedCanvas = null;
            UpdateCombatRoomStatus(next);
        }

        private void UpdateCombatRoomStatus(Scene scene)
        {
            try
            {
                string sceneName = scene.name;
                isInCombatRoom = sceneName != null && sceneName.StartsWith("Room_");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AutoUseSkill] UpdateCombatRoomStatus error: " + ex.Message);
                isInCombatRoom = false;
            }
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
                ShowFeedback(string.Format("Q 技能自动释放: {0}", AutoUseDict[0] ? "开启 (ON)" : "关闭 (OFF)"));
            }
            if (Input.GetKeyDown(KeyCode.F2))
            {
                AutoUseDict[1] = !AutoUseDict[1];
                ShowFeedback(string.Format("W 技能自动释放: {0}", AutoUseDict[1] ? "开启 (ON)" : "关闭 (OFF)"));
            }
            if (Input.GetKeyDown(KeyCode.F3))
            {
                AutoUseDict[2] = !AutoUseDict[2];
                ShowFeedback(string.Format("E 技能自动释放: {0}", AutoUseDict[2] ? "开启 (ON)" : "关闭 (OFF)"));
            }
            if (Input.GetKeyDown(KeyCode.F4))
            {
                AutoUseDict[3] = !AutoUseDict[3];
                ShowFeedback(string.Format("R 技能自动释放: {0}", AutoUseDict[3] ? "开启 (ON)" : "关闭 (OFF)"));
            }
            if (Input.GetKeyDown(KeyCode.F5))
            {
                Auto_Attack = !Auto_Attack;
                ShowFeedback(string.Format("自动普通攻击: {0}", Auto_Attack ? "开启 (ON)" : "关闭 (OFF)"));
            }

            // Local cache to guard against TOCTOU null reference
            Hero player = Player;
            if (player == null || (!AutoCastOutOfCombat && !player.isInCombat)) return;
            if (Time.time - lastSearchTime < SearchInterval) return;
            lastSearchTime = Time.time;

            ControlManager cm = controlManager;

            // Single search for closest enemy per interval to optimize performance
            Entity closestEnemy = FindClosestEnemy(player, 25f);

            TryAutoCastSkill(player, cm, closestEnemy);
            TryAutoAttack(player, closestEnemy);
        }

        private Entity FindClosestEnemy(Hero player, float maxRange)
        {
            if (player == null) return null;
            Entity closest = null;
            float minDistSq = maxRange * maxRange;
            Vector3 playerPos = player.agentPosition;

            try
            {
                if (NetworkedManagerBase<ActorManager>.instance != null && NetworkedManagerBase<ActorManager>.instance.allEntities != null)
                {
                    foreach (Entity e in NetworkedManagerBase<ActorManager>.instance.allEntities)
                    {
                        if (e == null || !e.isActive) continue;
                        if (player.GetRelation(e) != EntityRelation.Enemy) continue;
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
            catch (Exception ex)
            {
                Debug.LogWarning("[AutoUseSkill] FindClosestEnemy error: " + ex.Message);
            }

            return closest;
        }

        private void TryAutoCastSkill(Hero player, ControlManager cm, Entity closestEnemy)
        {
            if (player == null || cm == null) return;

            for (var i = 0; i <= 3; i++)
            {
                if (!AutoUseDict[i]) continue;
                AbilityTrigger abilityTrigger;
                if (!player.Ability.abilities.TryGetValue(i, out abilityTrigger)) continue;
                SkillTrigger skill = abilityTrigger as SkillTrigger;
                if (skill == null || skill.IsNullOrInactive()) continue;

                // Protect continuous channeling skills from being disrupted
                if (skill.Network_isCasting) continue;
                if (!skill.CanBeCast()) continue;

                float range = skill.currentConfig != null ? skill.currentConfig.effectiveRange : 8f;
                Entity target = cm.targetEnemy;

                if (target == null || !target.isActive || player.GetRelation(target) != EntityRelation.Enemy)
                {
                    if (closestEnemy != null && closestEnemy.isActive)
                    {
                        float dSq = (closestEnemy.agentPosition - player.agentPosition).sqrMagnitude;
                        if (dSq <= range * range)
                        {
                            target = closestEnemy;
                        }
                    }
                }

                if (target != null && skill.currentConfig != null && !skill.currentConfig.CheckRange(player, target))
                {
                    continue;
                }

                // If no enemy target in range, verify whether skill requires a target before casting
                if (target == null && skill.currentConfig != null && skill.currentConfig.castMethod != null)
                {
                    CastMethodType methodType = skill.currentConfig.castMethod.type;
                    if (methodType == CastMethodType.Target || methodType == CastMethodType.Cone || methodType == CastMethodType.Arrow)
                    {
                        // Directional / targeted skills should not fire into empty air
                        continue;
                    }
                }

                try
                {
                    if (target != null)
                    {
                        cm.CastAbility(skill, new CastInfo(player, target), false);
                    }
                    else
                    {
                        cm.CastAbilityAuto(skill);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[AutoUseSkill] CastAbility error: " + ex.Message);
                }
            }
        }

        private void TryAutoAttack(Hero player, Entity closestEnemy)
        {
            if (!Auto_Attack || player == null) return;
            AbilityTrigger attackAbility = player.Ability.attackAbility;
            if (attackAbility == null || attackAbility.IsNullOrInactive()) return;

            // Continuous channel check (e.g. Yubar laser beam): do not disrupt active channel!
            if (attackAbility.Network_isCasting) return;

            if (!attackAbility.CanBeCast()) return;

            float range = attackAbility.currentConfig != null ? attackAbility.currentConfig.effectiveRange : 4.5f;
            if (range < 2.5f) range = 2.5f;

            // 1. Native attack-move target finder
            Entity target = null;
            try
            {
                target = ActionAttackMove.FindAttackMoveTarget(player, player.agentPosition);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AutoUseSkill] FindAttackMoveTarget error: " + ex.Message);
            }

            // 2. Fallback to cached closest enemy within range
            if (target == null || !target.isActive || player.GetRelation(target) != EntityRelation.Enemy)
            {
                if (closestEnemy != null && closestEnemy.isActive)
                {
                    float dSq = (closestEnemy.agentPosition - player.agentPosition).sqrMagnitude;
                    if (dSq <= range * range)
                    {
                        target = closestEnemy;
                    }
                }
            }

            if (target == null || !target.isActive) return;

            // Range validation
            if (attackAbility.currentConfig != null && !attackAbility.currentConfig.CheckRange(player, target))
            {
                return;
            }

            // 3. Issue single native server attack command without canceling movement (cancelMovement = false)
            try
            {
                player.Control.CmdAttack(target, false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AutoUseSkill] CmdAttack error: " + ex.Message);
            }
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

                        if (cachedCanvas == null)
                        {
                            cachedCanvas = btn.GetComponentInParent<Canvas>();
                        }

                        if (cachedCanvas != null && cachedCanvas.renderMode != RenderMode.ScreenSpaceOverlay && cachedCanvas.worldCamera != null)
                        {
                            pos = RectTransformUtility.WorldToScreenPoint(cachedCanvas.worldCamera, pos);
                        }

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
            catch (Exception ex)
            {
                Debug.LogWarning("[AutoUseSkill] DrawSkillBadges error: " + ex.Message);
            }
        }

        private void OnGUI()
        {
            // If in Lobby, Traveler Settings, Constellations, or Main Menu: NEVER run badge checks or layout!
            if (!IsInCombatRoom)
            {
                if (ShowGUI)
                {
                    WindowRect = GUILayout.Window(WindowId, WindowRect, MenuGui, "Auto Use Skill", "box");
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
            WindowRect = GUILayout.Window(WindowId, WindowRect, MenuGui, "Auto Use Skill", "box");
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
            SearchInterval = (float)Math.Round(GUILayout.HorizontalSlider(SearchInterval, 0.03f, 0.5f, option), 2);
            GUILayout.EndVertical();
        }

        [ConsoleCommand("autoskill", "切换自动施法GUI显示")]
        public void CmdToggleGui()
        {
            ShowGUI = !ShowGUI;
        }
    }
}