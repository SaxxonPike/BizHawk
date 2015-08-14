﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;

using BizHawk.Common;
using BizHawk.Emulation.Common;
using BizHawk.Emulation.Common.IEmulatorExtensions;

namespace BizHawk.Client.Common
{
	class tsmState : IDisposable
	{
		static long state_id = 0;
		TasStateManager _manager;

		byte[] _state;
		int frame;
		long ID;

		public tsmState(TasStateManager manager, byte[] state)
		{
			_manager = manager;
			_state = state;
			
			//I still think this is a bad idea. IDs may need scavenging somehow
			if (state_id > long.MaxValue - 100)
				throw new InvalidOperationException();
			ID = System.Threading.Interlocked.Increment(ref state_id);
		}

		public byte[] State
		{
			get
			{
				if (_state != null)
					return _state;

				return _manager.ndbdatabase.FetchAll(ID.ToString());
			}
			set
			{
				if (_state != null)
				{
					_state = value;
					return;
				}

				_state = value;
				MoveToDisk();
			}
		}
		public int Length { get { return State.Length; } }

		public bool IsOnDisk { get { return _state == null; } }
		public void MoveToDisk()
		{
			if (IsOnDisk)
				return;

			_manager.ndbdatabase.Store(ID.ToString(), _state, 0, _state.Length);
			_state = null;
		}
		public void MoveToRAM()
		{
			if (!IsOnDisk)
				return;

			var key = ID.ToString();
			var ret = _manager.ndbdatabase.FetchAll(key);
			_manager.ndbdatabase.Release(key);
		}
		public void Dispose()
		{
			if (!IsOnDisk)
				return;

			_manager.ndbdatabase.Release(ID.ToString());
		}
	}

	/// <summary>
	/// Captures savestates and manages the logic of adding, retrieving, 
	/// invalidating/clearing of states.  Also does memory management and limiting of states
	/// </summary>
	public class TasStateManager : IDisposable
	{
		// TODO: pass this in, and find a solution to a stale reference (this is instantiated BEFORE a new core instance is made, making this one stale if it is simply set in the constructor
		private IStatable Core
		{
			get
			{
				return Global.Emulator.AsStatable();
			}
		}

		public Action<int> InvalidateCallback { get; set; }

		private void CallInvalidateCallback(int index)
		{
			if (InvalidateCallback != null)
			{
				InvalidateCallback(index);
			}
		}

		internal NDBDatabase ndbdatabase;
		private Guid guid = Guid.NewGuid();
		private SortedList<int, tsmState> States = new SortedList<int, tsmState>();
		private SortedList<int, SortedList<int, tsmState>> BranchStates = new SortedList<int, SortedList<int, tsmState>>();
		private int branches = 0;

		/// <summary>
		/// Checks if the state at frame in the given branch (-1 for current) has any duplicates.
		/// </summary>
		/// <returns>Returns the ID of the branch (-1 for current) of the first match. If no match, returns -2.</returns>
		private int stateHasDuplicate(int frame, int branch)
		{
			tsmState stateToMatch;
			if (branch == -1)
				stateToMatch = States[frame];
			else
			{
				stateToMatch = BranchStates[frame][branch];
				if (States.ContainsKey(frame) && States[frame] == stateToMatch)
					return -1;
			}

			for (int i = 0; i < branches; i++)
			{
				if (i == branch)
					continue;

				if (BranchStates.ContainsKey(frame))
				{
					SortedList<int, tsmState> stateList = BranchStates[frame];
					if (stateList != null && stateList.ContainsKey(i) && stateList[i] == stateToMatch)
						return i;
				}
			}

			return -2;
		}

		public string statePath
		{
			get
			{
				var basePath = PathManager.MakeAbsolutePath(Global.Config.PathEntries["Global", "TAStudio states"].Path, null);
				return Path.Combine(basePath, guid.ToString());
			}
		}

		private bool _isMountedForWrite;
		private readonly TasMovie _movie;
		private ulong _expectedStateSize = 0;

		private int _minFrequency = VersionInfo.DeveloperBuild ? 2 : 1;
		private const int _maxFrequency = 16;
		private int StateFrequency
		{
			get
			{
				int freq = (int)(_expectedStateSize / 65536);

				if (freq < _minFrequency)
				{
					return _minFrequency;
				}

				if (freq > _maxFrequency)
				{
					return _maxFrequency;
				}

				return freq;
			}
		}

		private int maxStates
		{ get { return (int)(Settings.Cap / _expectedStateSize); } }

		public TasStateManager(TasMovie movie)
		{
			_movie = movie;

			Settings = new TasStateManagerSettings(Global.Config.DefaultTasProjSettings);

			accessed = new List<int>();
		}

		public void Dispose()
		{
			if (ndbdatabase != null)
				ndbdatabase.Dispose();

			//States and BranchStates don't need cleaning because they would only contain an ndbdatabase entry which was demolished by the above
		}

		/// <summary>
		/// Mounts this instance for write access. Prior to that it's read-only
		/// </summary>
		public void MountWriteAccess()
		{
			if (_isMountedForWrite)
				return;

			_isMountedForWrite = true;

			int limit = 0;

			_expectedStateSize = (ulong)Core.SaveStateBinary().Length;

			if (_expectedStateSize > 0)
			{
				limit = maxStates;
			}

			States = new SortedList<int, tsmState>(limit);

			if(_expectedStateSize > int.MaxValue)
				throw new InvalidOperationException();
			ndbdatabase = new NDBDatabase(statePath, Settings.DiskCapacitymb * 1024 * 1024, (int)_expectedStateSize);
		}

		public TasStateManagerSettings Settings { get; set; }

		/// <summary>
		/// Retrieves the savestate for the given frame,
		/// If this frame does not have a state currently, will return an empty array
		/// </summary>
		/// <returns>A savestate for the given frame or an empty array if there isn't one</returns>
		public KeyValuePair<int, byte[]> this[int frame]
		{
			get
			{
				if (frame == 0 && _movie.StartsFromSavestate)
				{
					return new KeyValuePair<int, byte[]>(0, _movie.BinarySavestate);
				}

				if (States.ContainsKey(frame))
				{
					StateAccessed(frame);
					return new KeyValuePair<int, byte[]>(frame, States[frame].State);
				}

				return new KeyValuePair<int, byte[]>(-1, new byte[0]);
			}
		}
		private List<int> accessed;

		public byte[] InitialState
		{
			get
			{
				if (_movie.StartsFromSavestate)
				{
					return _movie.BinarySavestate;
				}

				return States[0].State;
			}
		}

		/// <summary>
		/// Requests that the current emulator state be captured 
		/// Unless force is true, the state may or may not be captured depending on the logic employed by "greenzone" management
		/// </summary>
		public void Capture(bool force = false)
		{
			bool shouldCapture = false;

			int frame = Global.Emulator.Frame;
			if (_movie.StartsFromSavestate && frame == 0) // Never capture frame 0 on savestate anchored movies since we have it anyway
			{
				shouldCapture = false;
			}
			else if (force)
			{
				shouldCapture = force;
			}
			else if (frame == 0) // For now, long term, TasMovie should have a .StartState property, and a tasproj file for the start state in non-savestate anchored movies
			{
				shouldCapture = true;
			}
			else if (_movie.Markers.IsMarker(frame + 1))
			{
				shouldCapture = true; // Markers shoudl always get priority
			}
			else
			{
				shouldCapture = frame - States.Keys.LastOrDefault(k => k < frame) >= StateFrequency;
			}

			if (shouldCapture)
			{
				SetState(frame, (byte[])Core.SaveStateBinary().Clone());
			}
		}

		private void MaybeRemoveState()
		{
			int shouldRemove = -1;
			if (Used + DiskUsed > Settings.CapTotal)
				shouldRemove = StateToRemove();
			if (shouldRemove != -1)
			{
				RemoveState(States.ElementAt(shouldRemove).Key);
			}

			if (Used > Settings.Cap)
			{
				int lastMemState = -1;
				do { lastMemState++; } while (States[accessed[lastMemState]] == null);
				MoveStateToDisk(accessed[lastMemState]);
			}
		}
		private int StateToRemove()
		{
			int markerSkips = maxStates / 3;

			int shouldRemove = _movie.StartsFromSavestate ? -1 : 0;
			do
			{
				shouldRemove++;

				// No need to have two savestates with only lag frames between them:
				// zero 05-aug-2015 - changed algorithm to iterate through States (a SortedList) instead of repeatedly call ElementAt (which is slow)
				//   previously : for (int i = shouldRemove; i < States.Count - 1; i++) if (AllLag(States.ElementAt(i).Key, States.ElementAt(i + 1).Key)) { shouldRemove = i; break; } }
				int ctr = 0;
				KeyValuePair<int, tsmState>? prior = null;
				foreach (var kvp in States)
				{
					ctr++;
					if (ctr < shouldRemove)
					{
						prior = kvp;
						continue;
					}

					if (prior.HasValue)
					{
						if (AllLag(prior.Value.Key, kvp.Key))
						{
							shouldRemove = ctr - 1;
							break;
						}
					}

					prior = kvp;
				}

				// Keep marker states
				markerSkips--;
				if (markerSkips < 0)
					shouldRemove = _movie.StartsFromSavestate ? 0 : 1;
			} while (_movie.Markers.IsMarker(States.ElementAt(shouldRemove).Key + 1) && markerSkips > -1);

			return shouldRemove;
		}
		private bool AllLag(int from, int upTo)
		{
			if (upTo >= Global.Emulator.Frame)
			{
				upTo = Global.Emulator.Frame - 1;
				if (!Global.Emulator.AsInputPollable().IsLagFrame)
					return false;
			}

			for (int i = from; i < upTo; i++)
			{
				if (!_movie[i].Lagged.Value)
					return false;
			}

			return true;
		}

		private void MoveStateToDisk(int index)
		{
			Used -= (ulong)States[index].Length;
			States[index].MoveToDisk();
		}
		private void MoveStateToMemory(int index)
		{
			States[index].MoveToRAM();
			Used += (ulong)States[index].Length;
		}

		internal void SetState(int frame, byte[] state)
		{
			MaybeRemoveState(); // Remove before adding so this state won't be removed.
			if (States.ContainsKey(frame))
			{
				if (stateHasDuplicate(frame, -1) != -2)
					Used += (ulong)state.Length;
				States[frame].State = state;
			}
			else
			{
				Used += (ulong)state.Length;
				States.Add(frame, new tsmState(this, state));
			}

			StateAccessed(frame);
		}
		private void RemoveState(int frame)
		{
			if (States[frame].IsOnDisk)
			{
				States[frame].Dispose();
			}
			else
				Used -= (ulong)States[frame].Length;

			States.RemoveAt(States.IndexOfKey(frame));
			accessed.Remove(frame);
		}
		private void StateAccessed(int index)
		{
			if (index == 0 && _movie.StartsFromSavestate)
				return;

			bool removed = accessed.Remove(index);
			accessed.Add(index);

			if (States[index].IsOnDisk)
			{
				if (!States[accessed[0]].IsOnDisk)
					MoveStateToDisk(accessed[0]);
				MoveStateToMemory(index);
			}

			if (!removed && accessed.Count > (int)(Used / _expectedStateSize))
				accessed.RemoveAt(0);
		}

		public bool HasState(int frame)
		{
			if (_movie.StartsFromSavestate && frame == 0)
			{
				return true;
			}

			return States.ContainsKey(frame);
		}

		/// <summary>
		/// Clears out all savestates after the given frame number
		/// </summary>
		public bool Invalidate(int frame)
		{
			bool anyInvalidated = false;

			if (Any())
			{
				if (!_movie.StartsFromSavestate && frame == 0) // Never invalidate frame 0 on a non-savestate-anchored movie
				{
					frame = 1;
				}

				var statesToRemove = States
					.Where(x => x.Key >= frame)
					.ToList();

				anyInvalidated = statesToRemove.Any();

				foreach (var state in statesToRemove)
				{
					if (state.Value.IsOnDisk)
					{
						state.Value.Dispose();
					}
					else
						Used -= (ulong)state.Value.Length;

					accessed.Remove(state.Key);
					States.Remove(state.Key);
				}

				CallInvalidateCallback(frame);
			}

			return anyInvalidated;
		}

		/// <summary>
		/// Clears all state information
		/// </summary>
		/// 
		public void Clear()
		{
			States.Clear();
			accessed.Clear();
			Used = 0;
			clearDiskStates();
			ndbdatabase.Clear();
		}
		public void ClearStateHistory()
		{
			if (States.Any())
			{
				KeyValuePair<int, tsmState> power = States.FirstOrDefault(s => s.Key == 0);
				StateAccessed(power.Key);
				if (power.Value.IsOnDisk) // TODO: Is this needed?
					power = States.FirstOrDefault(s => s.Key == 0);

				States.Clear();
				accessed.Clear();

				if (power.Value != null) // savestate-anchored movie?
				{
					SetState(0, power.Value.State);
					Used = (ulong)power.Value.Length;
				}
				else
					Used = 0;

				clearDiskStates();
			}
		}
		private void clearDiskStates()
		{
			if (ndbdatabase != null)
				ndbdatabase.Clear();
		}

		// TODO: save/load BranchStates
		public void Save(BinaryWriter bw)
		{
			List<int> noSave = ExcludeStates();

			bw.Write(States.Count - noSave.Count);
			for (int i = 0; i < States.Count; i++)
			{
				if (noSave.Contains(i))
					continue;

				StateAccessed(States.ElementAt(i).Key);
				KeyValuePair<int, tsmState> kvp = States.ElementAt(i);
				bw.Write(kvp.Key);
				bw.Write(kvp.Value.Length);
				bw.Write(kvp.Value.State);
			}
		}
		private List<int> ExcludeStates()
		{
			List<int> ret = new List<int>();

			ulong saveUsed = Used + DiskUsed;
			int index = -1;
			while (saveUsed > (ulong)Settings.DiskSaveCapacitymb * 1024 * 1024)
			{
				do
				{
					index++;
				} while (_movie.Markers.IsMarker(States.ElementAt(index).Key + 1));
				ret.Add(index);
				if (States.ElementAt(index).Value.IsOnDisk)
					saveUsed -= _expectedStateSize;
				else
					saveUsed -= (ulong)States.ElementAt(index).Value.Length;
			}

			// If there are enough markers to still be over the limit, remove marker frames
			index = -1;
			while (saveUsed > (ulong)Settings.DiskSaveCapacitymb * 1024 * 1024)
			{
				index++;
				ret.Add(index);
				if (States.ElementAt(index).Value.IsOnDisk)
					saveUsed -= _expectedStateSize;
				else
					saveUsed -= (ulong)States.ElementAt(index).Value.Length;
			}

			return ret;
		}

		public void Load(BinaryReader br)
		{
			States.Clear();
			//if (br.BaseStream.Length > 0)
			//{ BaseStream.Length does not return the expected value.
			int nstates = br.ReadInt32();
			for (int i = 0; i < nstates; i++)
			{
				int frame = br.ReadInt32();
				int len = br.ReadInt32();
				byte[] data = br.ReadBytes(len);
				SetState(frame, data);
				//States.Add(frame, data);
				//Used += len;
			}
			//}
		}

		public KeyValuePair<int, byte[]> GetStateClosestToFrame(int frame)
		{
			var s = States.LastOrDefault(state => state.Key < frame);

			return this[s.Key];
		}

		// Map:
		// 4 bytes - total savestate count
		//[Foreach state]
		// 4 bytes - frame
		// 4 bytes - length of savestate
		// 0 - n savestate

		private ulong Used
		{
			get;
			set;
		}
		private ulong DiskUsed
		{
			get
			{
				if (ndbdatabase == null) return 0;
				else return (ulong)ndbdatabase.Consumed;
			}
		}

		public int StateCount
		{
			get
			{
				return States.Count;
			}
		}

		public bool Any()
		{
			if (_movie.StartsFromSavestate)
			{
				return States.Count > 0;
			}

			return States.Count > 1;
		}

		public int LastKey
		{
			get
			{
				if (States.Count == 0)
				{
					return 0;
				}

				return States.Last().Key;
			}
		}

		public int LastEmulatedFrame
		{
			get
			{
				if (StateCount > 0)
				{
					return LastKey;
				}

				return 0;
			}
		}

		#region "Branches"

		public void AddBranch()
		{
			foreach (KeyValuePair<int, tsmState> kvp in States)
			{
				if (!BranchStates.ContainsKey(kvp.Key))
					BranchStates.Add(kvp.Key, new SortedList<int, tsmState>());
				SortedList<int, tsmState> stateList = BranchStates[kvp.Key];
				if (stateList == null)
				{
					stateList = new SortedList<int, tsmState>();
					BranchStates[kvp.Key] = stateList;
				}
				stateList.Add(branches, kvp.Value);
			}
			branches++;
		}

		public void RemoveBranch(int index)
		{
			foreach (KeyValuePair<int, SortedList<int, tsmState>> kvp in BranchStates)
			{
				SortedList<int, tsmState> stateList = kvp.Value;
				if (stateList == null)
					continue;

				if (stateHasDuplicate(kvp.Key, index) == -2)
				{
					if (stateList[index].IsOnDisk)
					{ }
					else
						Used -= (ulong)stateList[index].Length;
				}

				stateList.Remove(index);
				if (stateList.Count == 0)
					BranchStates[kvp.Key] = null;
			}
			branches--;
		}

		public void UpdateBranch(int index)
		{
			// RemoveBranch
			foreach (KeyValuePair<int, SortedList<int, tsmState>> kvp in BranchStates)
			{
				SortedList<int, tsmState> stateList = kvp.Value;
				if (stateList == null)
					continue;

				if (stateHasDuplicate(kvp.Key, index) == -2)
				{
					if (stateList[index].IsOnDisk)
					{ }
					else
						Used -= (ulong)stateList[index].Length;
				}

				stateList.Remove(index);
				if (stateList.Count == 0)
					BranchStates[kvp.Key] = null;
			}

			// AddBranch
			foreach (KeyValuePair<int, tsmState> kvp in States)
			{
				if (!BranchStates.ContainsKey(kvp.Key))
					BranchStates.Add(kvp.Key, new SortedList<int, tsmState>());
				SortedList<int, tsmState> stateList = BranchStates[kvp.Key];
				if (stateList == null)
				{
					stateList = new SortedList<int, tsmState>();
					BranchStates[kvp.Key] = stateList;
				}
				stateList.Add(index, kvp.Value);
			}
		}

		public void LoadBranch(int index)
		{
			Invalidate(0); // Not a good way of doing it?
			foreach (KeyValuePair<int, SortedList<int, tsmState>> kvp in BranchStates)
			{
				if (kvp.Key == 0 && States.ContainsKey(0))
					continue; // TODO: It might be a better idea to just not put state 0 in BranchStates.

				if (kvp.Value.ContainsKey(index))
					SetState(kvp.Key, kvp.Value[index].State);
			}
		}

		#endregion
	}
}
