/// Copyright (C) 2012-2014 Soomla Inc.
///
/// Licensed under the Apache License, Version 2.0 (the "License");
/// you may not use this file except in compliance with the License.
/// You may obtain a copy of the License at
///
///      http://www.apache.org/licenses/LICENSE-2.0
///
/// Unless required by applicable law or agreed to in writing, software
/// distributed under the License is distributed on an "AS IS" BASIS,
/// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
/// See the License for the specific language governing permissions and
/// limitations under the License.

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Soomla.Levelup {

	/// <summary>
	/// This is the top level container for the unity-levelup model and definitions.
	/// It stores the configurations of the game's world-hierarchy and provides
	/// lookup functions for levelup model elements.
	/// </summary>
	public class LevelUp {

		public static readonly string DB_KEY_PREFIX = "soomla.levelup.";

		private const string TAG = "SOOMLA LevelUp";

		/// <summary>
		/// The instance of <c>LevelUp</c> for this game.
		/// </summary>
		private static LevelUp instance = null;

		/// <summary>
		/// Initial <c>World</c> to begin the game.
		/// </summary>
		public World InitialWorld;

		/// <summary>
		/// Potential rewards of the <c>InitialWorld</c>.
		/// </summary>
		public Dictionary<string, Reward> Rewards;

		/// <summary>
		/// Initializes the specified <c>InitialWorld</c> and rewards.
		/// </summary>
		/// <param name="initialWorld">Initial world.</param>
		/// <param name="rewards">Rewards.</param>
		public void Initialize(World initialWorld, List<Reward> rewards = null) {
			InitialWorld = initialWorld;
//			save();

			if (rewards != null) {
				Dictionary<string, Reward> rewardMap = new Dictionary<string, Reward> ();
				foreach (Reward reward in rewards) {
						rewardMap.Add (reward.ID, reward);
				}
				Rewards = rewardMap;
			}

			WorldStorage.InitLevelUp(this.toJSONObject());
		}

		/// <summary>
		/// Retrieves the reward with the given ID.
		/// </summary>
		/// <returns>The reward that was fetched.</returns>
		/// <param name="rewardId">ID of the <c>Reward</c> to be fetched.</param>
		public Reward GetReward(string rewardId) {
			if (Rewards == null) {
				return null;
			}

			Reward reward = null;
			Rewards.TryGetValue(rewardId, out reward);
			return reward;
		}

		/// <summary>
		/// Retrieves the <c>Score</c> with the given score ID.
		/// </summary>
		/// <returns>The score.</returns>
		/// <param name="scoreId">ID of the <c>Score</c> to be fetched.</param>
		public Score GetScore(string scoreId) {
			Score retScore = null;
			InitialWorld.Scores.TryGetValue(scoreId, out retScore);
			if (retScore == null) {
				retScore = fetchScoreFromWorlds(scoreId, InitialWorld.InnerWorldsMap);
			}
			
			return retScore;
		}

		/// <summary>
		/// Retrieves the <c>World</c> with the given world ID.
		/// </summary>
		/// <returns>The world.</returns>
		/// <param name="worldId">World ID of the <c>Score</c> to be fetched.</param>
		public World GetWorld(string worldId) {
			if (InitialWorld.ID == worldId) {
				return InitialWorld;
			}

			return fetchWorld(worldId, InitialWorld.InnerWorldsMap);
		}


		public Level GetLevel(string levelId) {
			return GetWorld(levelId) as Level;
		}

		/// <summary>
		/// Retrieves the <c>Gate</c> with the given ID.
		/// </summary>
		/// <returns>The gate.</returns>
		/// <param name="gateId">ID of the <c>Gate</c> to be fetched.</param>
		public Gate GetGate(string gateId) {
			if (InitialWorld.Gate != null &&
			    InitialWorld.Gate.ID == gateId) {
				return InitialWorld.Gate;
			}

			Gate gate = fetchGate(gateId, InitialWorld.Missions);
			if (gate != null) {
				return gate;
			}

			return fetchGate(gateId, InitialWorld.InnerWorldsList);
		}

		/// <summary>
		/// Retrieves the <c>Mission</c> with the given ID.
		/// </summary>
		/// <returns>The mission.</returns>
		/// <param name="missionId">ID of the <c>Mission</c> to be fetched.</param>
		public Mission GetMission(string missionId) {
			Mission mission = (from m in InitialWorld.Missions
			 where m.ID == missionId
			 select m).SingleOrDefault();

			if (mission == null) {
				return fetchMission(missionId, InitialWorld.InnerWorldsList);
			}

			return mission;
		}

		/// <summary>
		/// Counts all the <c>Level</c>s in all <c>World</c>s and inner <c>World</c>s 
		/// starting from the <c>InitialWorld</c>.
		/// </summary>
		/// <returns>The number of levels in all worlds and their inner worlds.</returns>
		public int GetLevelCount() {
			return GetLevelCountInWorld(InitialWorld);
		}

		/// <summary>
		/// Counts all the <c>Level</c>s in all <c>World</c>s and inner <c>World</c>s 
		/// starting from the given <c>World</c>.
		/// </summary>
		/// <param name="world">The world to examine.</param>
		/// <returns>The number of levels in the given world and its inner worlds.</returns>
		public int GetLevelCountInWorld(World world) {
			int count = 0;
			foreach (World initialWorld in world.InnerWorldsMap.Values) {
				count += getRecursiveCount(initialWorld, (World innerWorld) => {
					return innerWorld.GetType() == typeof(Level);
				});
			}
			return count;
		}

		/// <summary>
		/// Counts all <c>World</c>s and their inner <c>World</c>s with or without their  
		/// <c>Level</c>s according to the given <c>withLevels</c>.
		/// </summary>
		/// <param name="withLevels">Indicates whether to count <c>Level</c>s also.</param>
		/// <returns>The number of <c>World</c>s, and optionally their inner <c>Level</c>s.</returns>
		public int GetWorldCount(bool withLevels) {
			return getRecursiveCount(InitialWorld, (World innerWorld) => {
				return withLevels ?
					(innerWorld.GetType() == typeof(World) || innerWorld.GetType() == typeof(Level)) :
						(innerWorld.GetType() == typeof(World));
			});
		}

		/// <summary>
		/// Counts all completed <c>Level</c>s.
		/// </summary>
		/// <returns>The number of completed <c>Level</c>s and their inner completed 
		/// <c>Level</c>s.</returns>
		public int GetCompletedLevelCount() {
			return getRecursiveCount(InitialWorld, (World innerWorld) => {
				return innerWorld.GetType() == typeof(Level) && innerWorld.IsCompleted();
			});
		}

		/// <summary>
		/// Counts the number of completed <c>World</c>s.
		/// </summary>
		/// <returns>The number of completed <c>World</c>s and their inner completed 
		/// <c>World</c>s.</returns>
		public int GetCompletedWorldCount() {
			return getRecursiveCount(InitialWorld, (World innerWorld) => {
				return innerWorld.GetType() == typeof(World) && innerWorld.IsCompleted();
			});
		}

		/// <summary>
		/// Retrieves this instance of <c>LevelUp</c>. Used when initializing LevelUp.
		/// </summary>
		/// <returns>This instance of <c>LevelUp</c>.</returns>
		public static LevelUp GetInstance() {
			if (instance == null) {
				instance = new LevelUp();
			}
			return instance;
		}


		/** PRIVATE FUNCTIONS **/

		private LevelUp() {}

		// NOTE: Not sure we need a save function.

//		private void save() {
//			string lu_json = toJSONObject().print();
//			SoomlaUtils.LogDebug(TAG, "saving LevelUp to DB. json is: " + lu_json);
//			string key = DB_KEY_PREFIX + "model";
//
//			// TODO: save on Android and iOS with KeyValueStorage
//			// KeyValueStorage.setValue(key, lu_json);
//		}

		private JSONObject toJSONObject() {
			JSONObject obj = new JSONObject(JSONObject.Type.OBJECT);

			obj.AddField(LUJSONConsts.LU_MAIN_WORLD, InitialWorld.toJSONObject());

			JSONObject rewardsArr = new JSONObject(JSONObject.Type.ARRAY);

			if (this.Rewards != null)
				foreach(Reward reward in this.Rewards.Values)
					rewardsArr.Add(reward.toJSONObject());

			obj.AddField(JSONConsts.SOOM_REWARDS, rewardsArr);

			return obj;
		}

		private Score fetchScoreFromWorlds(string scoreId, Dictionary<string, World> worlds) {
			Score retScore = null;
			foreach (World world in worlds.Values) {
				world.Scores.TryGetValue(scoreId, out retScore);
				if (retScore == null) {
					retScore = fetchScoreFromWorlds(scoreId, world.InnerWorldsMap);
				}
				if (retScore != null) {
					break;
				}
			}
			
			return retScore;
		}

		private World fetchWorld(string worldId, Dictionary<string, World> worlds) {
			World retWorld;
			worlds.TryGetValue(worldId, out retWorld);
			if (retWorld == null) {
				foreach (World world in worlds.Values) {
					retWorld = fetchWorld(worldId, world.InnerWorldsMap);
					if (retWorld != null) {
						break;
					}
				}
			}
			
			return retWorld;
		}

		private Mission fetchMission(string missionId, IEnumerable<World> worlds) {
			foreach (World world in worlds) {
				Mission mission = (from m in world.Missions
				                   where m.ID == missionId
				                   select m).SingleOrDefault();
				if (mission != null) {
					return mission;
				}
				mission = fetchMission(missionId, world.InnerWorldsList);
				if (mission != null) {
					return mission;
				}
			}

			return null;
		}

		private Gate fetchGate(string gateId, IEnumerable<World> worlds) {
			if (worlds == null) {
				return null;
			}

			Gate retGate = null;
			foreach (var world in worlds) {
				retGate = fetchGate(gateId, world.Gate);
				if (retGate != null) {
					return retGate;
				}
			}

			if (retGate == null) {
				foreach (World world in worlds) {
					retGate = fetchGate(gateId, world.Missions);
					if (retGate != null) {
						break;
					}

					retGate = fetchGate(gateId, world.InnerWorldsList);
					if (retGate != null) {
						break;
					}
				}
			}

			
			return retGate;
		}


		private Gate fetchGate(string gateId, List<Mission> missions) {

			Gate retGate = null;
			foreach (var mission in missions) {
				retGate = fetchGate(gateId, mission.Gate);
				if (retGate != null) {
					return retGate;
				}
			}

			if (retGate == null) {
				foreach (Mission mission in missions) {
					if (mission is Challenge) {
						retGate = fetchGate(gateId, ((Challenge)mission).Missions);
						if (retGate != null) {
							break;
						}
					}
				}
			}

			return retGate;
		}


		private Gate fetchGate(string gateId, Gate targetGate) {
			if (targetGate == null) {
				return null;
			}

			if ((targetGate != null) && (targetGate.ID == gateId)) {
				return targetGate;
			}

			Gate result = null;
			GatesList gatesList = targetGate as GatesList;

			if (gatesList != null) {
				for (int i = 0; i < gatesList.Count; i++) {
					result = fetchGate(gateId, gatesList[i]);
					if (result != null) {
						return result;
					}
				}
			}
			
			return result;
		}


		private int getRecursiveCount(World world, Func<World, bool> isAccepted) {
			int count = 0;
			
			// If the predicate is true, increment
			if (isAccepted(world)) {
				count++;
			}
			
			foreach (World innerWorld in world.InnerWorldsMap.Values) {
				
				// Recursively count for inner world
				count += getRecursiveCount(innerWorld, isAccepted);
			}
			return count;
		}

	}
}

