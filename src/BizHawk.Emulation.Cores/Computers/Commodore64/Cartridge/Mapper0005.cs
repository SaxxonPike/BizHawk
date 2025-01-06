using System.Collections.Generic;
using System.Linq;
using BizHawk.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge
{
	internal sealed class Mapper0005 : CartridgeDevice
	{
		private readonly byte[][] _banksA; // 8000

		private readonly byte[][] _banksB = new byte[0][]; // A000

		private byte _bankMask;

		private byte _bankNumber;

		private byte[] _currentBankA;

		private byte[] _currentBankB;

		private readonly byte[] _dummyBank;

		public Mapper0005(IReadOnlyList<CartridgeChip> chips)
		{
			// build dummy bank
			_dummyBank = new byte[0x2000];
			_dummyBank.AsSpan().Fill(0xFF);

			switch (chips.Count)
			{
				case 64:
					pinGame = true;
					pinExRom = false;
					_bankMask = 0x3F;
					_banksA = new byte[64][];
					break;
				case 32:
					// this specific config is a weird exception
					pinGame = false;
					pinExRom = false;
					_bankMask = 0x0F;
					_banksA = new byte[16][];
					_banksB = new byte[16][];
					break;
				case 16:
					pinGame = true;
					pinExRom = false;
					_bankMask = 0x0F;
					_banksA = new byte[16][];
					break;
				case 8:
					pinGame = true;
					pinExRom = false;
					_bankMask = 0x07;
					_banksA = new byte[8][];
					break;
				case 4:
					pinGame = true;
					pinExRom = false;
					_bankMask = 0x03;
					_banksA = new byte[4][];
					break;
				case 2:
					pinGame = true;
					pinExRom = false;
					_bankMask = 0x01;
					_banksA = new byte[2][];
					break;
				case 1:
					pinGame = true;
					pinExRom = false;
					_bankMask = 0x00;
					_banksA = new byte[1][];
					break;
				default:
					throw new Exception("This looks like an Ocean cartridge but cannot be loaded...");
			}

			// for safety, initialize all banks to dummy
			_banksA.AsSpan().Fill(_dummyBank);
			_banksB.AsSpan().Fill(_dummyBank);

			// now load in the banks
			foreach (var chip in chips)
			{
				switch (chip.Address)
				{
					case 0x8000:
						_banksA[chip.Bank & _bankMask] = chip.ConvertDataToBytes();
						break;
					case 0xA000:
					case 0xE000:
						_banksB[chip.Bank & _bankMask] = chip.ConvertDataToBytes();
						break;
				}
			}

			BankSet(0);
		}

		protected override void SyncStateInternal(Serializer ser)
		{
			ser.Sync("BankMask", ref _bankMask);
			ser.Sync("BankNumber", ref _bankNumber);

			if (ser.IsReader)
			{
				BankSet(_bankNumber);
			}
		}

		private void BankSet(int index)
		{
			_bankNumber = unchecked((byte) (index & _bankMask));
			_currentBankA = !pinExRom ? _banksA[_bankNumber] : _dummyBank;
			_currentBankB = !pinGame ? _banksB[_bankNumber] : _dummyBank;
		}

		public override int Peek8000(int addr)
		{
			return _currentBankA[addr];
		}

		public override int PeekA000(int addr)
		{
			return _currentBankB[addr];
		}

		public override void PokeDE00(int addr, int val)
		{
			if (addr == 0x00)
			{
				BankSet(val);
			}
		}

		public override int Read8000(int addr)
		{
			return _currentBankA[addr];
		}

		public override int ReadA000(int addr)
		{
			return _currentBankB[addr];
		}

		public override void WriteDE00(int addr, int val)
		{
			if (addr == 0x00)
			{
				BankSet(val);
			}
		}
	}
}
