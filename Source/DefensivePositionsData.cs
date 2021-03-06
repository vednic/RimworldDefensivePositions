﻿using System.Collections.Generic;
using HugsLib.Utils;
using UnityEngine;
using Verse;

namespace DefensivePositions {
	// Contains the data stored within a game save
	public class DefensivePositionsData : UtilityWorldObject {
		public bool advancedModeEnabled;
		public int lastAdvancedControlUsed;
		public Dictionary<int, PawnSavedPositionHandler> handlers = new Dictionary<int, PawnSavedPositionHandler>();
		public List<PawnSquad> pawnSquads = new List<PawnSquad>();
		
		public override void ExposeData() {
			base.ExposeData();
			Scribe_Values.LookValue(ref advancedModeEnabled, "advancedModeEnabled", false);
			Scribe_Values.LookValue(ref lastAdvancedControlUsed, "lastAdvancedControlUsed", 0);
			Scribe_Collections.LookDictionary(ref handlers, "handlers", LookMode.Value, LookMode.Deep);
			Scribe_Collections.LookList(ref pawnSquads, "pawnSquads", LookMode.Deep);
			if (Scribe.mode == LoadSaveMode.LoadingVars) {
				if (handlers == null) handlers = new Dictionary<int, PawnSavedPositionHandler>();
				if (pawnSquads == null) pawnSquads = new List<PawnSquad>();
				lastAdvancedControlUsed = Mathf.Clamp(lastAdvancedControlUsed, 0, PawnSavedPositionHandler.NumAdvancedPositionButtons - 1);
			}
		}
	}
}