using System.Collections.Generic;

using BizHawk.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge
{
	internal sealed class Mapper0012 : CartridgeDevice
	{
		private readonly byte[] _bankMain;

		private readonly byte[][] _bankHigh;

		private byte[] _bankHighSelected;

		private byte _bankIndex;

		// Zaxxon and Super Zaxxon cartridges
		// - read to 8xxx selects bank 0 in A000-BFFF
		// - read to 9xxx selects bank 1 in A000-BFFF
		public Mapper0012(IEnumerable<CartridgeChip> chips)
		{
			_bankMain = new byte[0x2000];
			_bankHigh = new byte[2][];
			var dummyBank = new byte[0x2000];

			// create dummy bank just in case
			dummyBank.AsSpan().Fill(0xFF);

			_bankHigh[0] = dummyBank;
			_bankHigh[1] = dummyBank;

			// load in the banks
			foreach (var chip in chips)
			{
				if (chip.Address == 0x8000)
				{
					Array.Copy(chip.ConvertDataToBytes(), _bankMain, 0x1000);
				}
				else if ((chip.Address == 0xA000 || chip.Address == 0xE000) && chip.Bank < 2)
				{
					_bankHigh[chip.Bank] = chip.ConvertDataToBytes();
				}
			}

			// mirror the main rom from 8000 to 9000
			_bankMain.AsSpan(0, 0x1000).CopyTo(_bankMain.AsSpan(0x1000));

			// set both pins low for 16k rom config
			pinExRom = false;
			pinGame = false;
		}

		protected override void SyncStateInternal(Serializer ser)
		{
			ser.Sync("BankIndex", ref _bankIndex);
			if (ser.IsReader)
			{
				_bankHighSelected = _bankHigh[_bankIndex];
			}
		}

		public override int Peek8000(int addr)
		{
			return _bankMain[addr];
		}

		public override int PeekA000(int addr)
		{
			return _bankHighSelected[addr];
		}

		public override int Read8000(int addr)
		{
			_bankIndex = unchecked((byte) ((addr & 0x1000) >> 12));
			_bankHighSelected = _bankHigh[_bankIndex];
			return _bankMain[addr];
		}

		public override int ReadA000(int addr)
		{
			return _bankHighSelected[addr];
		}
	}
}
