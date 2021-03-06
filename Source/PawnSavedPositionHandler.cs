﻿using System.Collections.Generic;
using HugsLib.Utils;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DefensivePositions {
	/**
	 * This is where the gizmos are displayed and the positions are stored for saving.
	 */
	[StaticConstructorOnStartup]
	public class PawnSavedPositionHandler : IExposable {
		public const int NumAdvancedPositionButtons = 4;
		private const int InvalidMapValue = -1;

		private static readonly Texture2D UITex_Basic = ContentFinder<Texture2D>.Get("UIPositionLarge");
		private static readonly Texture2D[] UITex_AdvancedIcons;
		static PawnSavedPositionHandler() {
			UITex_AdvancedIcons = new Texture2D[NumAdvancedPositionButtons];
			for (int i = 0; i < UITex_AdvancedIcons.Length; i++) {
				UITex_AdvancedIcons[i] = ContentFinder<Texture2D>.Get("UIPositionSmall_"+(i+1));
			}
		}

		public Pawn Owner { get; set; }

		// --- saved fields ---
		private List<IntVec3> savedPositions; // the positions saved in the 4 slots for this pawn
		private List<int> originalMaps; // the map ids these positions were saved on
		// ---

		public PawnSavedPositionHandler() {
			InitalizePositionList();
		}

		public void ExposeData() {
			Scribe_Collections.LookList(ref savedPositions, "savedPositions", LookMode.Value);
			Scribe_Collections.LookList(ref originalMaps, "originalMaps", LookMode.Value);
			if (Scribe.mode == LoadSaveMode.LoadingVars && savedPositions == null) {
				InitalizePositionList();
			}
		}

		public bool TrySendPawnToPositionByHotkey() {
			var index = GetHotkeyControlIndex();
			var position = savedPositions[index];
			if(!PawnHasValidSavedPositionOnMap(Owner, index)) return false;
			DraftPawnToPosition(Owner, position);
			return true;
		}

		public Command GetGizmo(Pawn forPawn) {
			Owner = forPawn;
			if (DefensivePositionsManager.Instance.AdvancedModeEnabled) {
				return new Gizmo_QuadButtonPanel {
					iconTextures = UITex_AdvancedIcons,
					iconClickAction = OnAdvancedGizmoClick,
					hotkeyAction = OnAdvancedHotkeyDown,
					hotKey = HotkeyDefOf.DefensivePositionGizmo,
					defaultLabel = "DefPos_advanced_label".Translate(),
					defaultDesc = "DefPos_advanced_desc".Translate(),
					activateSound = SoundDefOf.TickTiny
				};
			} else {
				return new Command_Action {
					defaultLabel = "DefPos_basic_label".Translate(),
					defaultDesc = "DefPos_basic_desc".Translate(),
					hotKey = HotkeyDefOf.DefensivePositionGizmo,
					action = OnBasicGizmoAction,
					icon = UITex_Basic,
					activateSound = SoundDefOf.TickTiny
				};
			}
		}

		private void OnAdvancedHotkeyDown() {
			var controlToActivate = GetHotkeyControlIndex();
			HandleControlInteraction(controlToActivate);
		}

		private void OnAdvancedGizmoClick(int controlIndex) {
			DefensivePositionsManager.Instance.LastAdvancedControlUsed = controlIndex;
			HandleControlInteraction(controlIndex);
		}

		private void OnBasicGizmoAction() {
			HandleControlInteraction(0);
		}

		private int GetHotkeyControlIndex() {
			return DefensivePositionsManager.Instance.FirstSlotHotkeySetting.Value ? 0 : DefensivePositionsManager.Instance.LastAdvancedControlUsed;
		}

		private void HandleControlInteraction(int controlIndex) {
			var manager = DefensivePositionsManager.Instance;
			if (HugsLibUtility.ShiftIsHeld) {
				// save new spot
				SetDefensivePosition(Owner, controlIndex);
				manager.Reporter.ReportPawnInteraction(ScheduledReportManager.ReportType.SavedPosition, Owner, true, controlIndex);
			} else if (HugsLibUtility.ControlIsHeld) {
				// unset saved spot
				var hadPosition = DiscardSavedPosition(controlIndex);
				manager.Reporter.ReportPawnInteraction(ScheduledReportManager.ReportType.ClearedPosition, Owner, hadPosition, controlIndex);
			} else if (HugsLibUtility.AltIsHeld) {
				// switch mode
				manager.ScheduleAdvancedModeToggle();
			} else {
				// draft and send to saved spot
				var spot = savedPositions[controlIndex];
				if (PawnHasValidSavedPositionOnMap(Owner, controlIndex)) {
					DraftPawnToPosition(Owner, spot);
					manager.Reporter.ReportPawnInteraction(ScheduledReportManager.ReportType.SentToSavedPosition, Owner, true, controlIndex);
				} else {
					manager.Reporter.ReportPawnInteraction(ScheduledReportManager.ReportType.SentToSavedPosition, Owner, false, controlIndex);
				}
			}
		}

		private void SetDefensivePosition(Pawn pawn, int postionIndex) {
			var targetPos = pawn.Position;
			var curPawnJob = pawn.jobs.curJob;
			if (pawn.Drafted && curPawnJob != null && curPawnJob.def == JobDefOf.Goto) {
				targetPos = curPawnJob.targetA.Cell;
			}
			savedPositions[postionIndex] = targetPos;
			originalMaps[postionIndex] = pawn.Map.uniqueID;
		}

		// ensures that control index has a saved position and that position was saved on the map the pawn is on
		private bool PawnHasValidSavedPositionOnMap(Pawn pawn, int controlIndex) {
			return savedPositions[controlIndex].IsValid && originalMaps[controlIndex] == pawn.Map.uniqueID;
		}

		private bool DiscardSavedPosition(int controlIndex) {
			var hadPosition = savedPositions[controlIndex].IsValid;
			savedPositions[controlIndex] = IntVec3.Invalid;
			originalMaps[controlIndex] = InvalidMapValue;
			return hadPosition;
		}

		private void DraftPawnToPosition(Pawn pawn, IntVec3 position) {
			if (!pawn.IsColonistPlayerControlled || pawn.Downed || pawn.drafter == null) return;
			if (!pawn.Drafted) {
				pawn.drafter.Drafted = true;
				DefensivePositionsManager.Instance.ScheduleSoundOnCamera(SoundDefOf.DraftOn);
			}
			var turret = TryFindMannableGunAtPosition(pawn, position);
			if (turret != null) {
				var newJob = new Job(JobDefOf.ManTurret, turret.parent);
				pawn.jobs.TryTakeOrderedJob(newJob);
			} else {
				var intVec = RCellFinder.BestOrderedGotoDestNear(position, pawn);
				var job = new Job(JobDefOf.Goto, intVec) {playerForced = true};
				pawn.jobs.TryTakeOrderedJob(job);
				MoteMaker.MakeStaticMote(intVec, pawn.Map, ThingDefOf.Mote_FeedbackGoto);
			}
		}

		// check cardinal adjacent cells for mannable things
		private CompMannable TryFindMannableGunAtPosition(Pawn forPawn, IntVec3 position) {
			if (!forPawn.RaceProps.ToolUser) return null;
			var cardinals = GenAdj.CardinalDirections;
			for (int i = 0; i < cardinals.Length; i++) {
				var things = forPawn.Map.thingGrid.ThingsListAt(cardinals[i] + position);
				for (int j = 0; j < things.Count; j++) {
					var thing = things[j] as ThingWithComps;
					if (thing == null) continue;
					var comp = thing.GetComp<CompMannable>();
					if (comp == null || thing.InteractionCell != position) continue;
					var props = comp.Props;
					if (props == null || props.manWorkType == WorkTags.None || forPawn.story == null || forPawn.story.WorkTagIsDisabled(props.manWorkType)) continue;
					return comp;
				}
			}
			return null;
		}

		private void InitalizePositionList() {
			savedPositions = new List<IntVec3>(NumAdvancedPositionButtons);
			originalMaps = new List<int>(NumAdvancedPositionButtons);
			for (int i = 0; i < NumAdvancedPositionButtons; i++) {
				savedPositions.Add(IntVec3.Invalid);
				originalMaps.Add(InvalidMapValue);
			}
		}
	}
}