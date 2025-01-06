namespace BizHawk.Emulation.Cores.Computers.Commodore64.MOS;

public sealed partial class Cia
{
	private interface IPort
	{
		byte ReadPra(byte pra, byte ddra, byte prb, byte ddrb);
		byte ReadPrb(byte pra, byte ddra, byte prb, byte ddrb);

		// If an IPort needs to save state we can do it with something like this:
		// void SyncState(Serializer ser);
	}

	private sealed class DisconnectedPort : IPort
	{
		public byte ReadPra(byte pra, byte ddra, byte prb, byte ddrb)
		{
			return unchecked((byte)(pra | ~ddra));
		}

		public byte ReadPrb(byte pra, byte ddra, byte prb, byte ddrb)
		{
			return unchecked((byte) (prb | ~ddrb));
		}
	}

	private sealed class JoystickKeyboardPort : IPort
	{
		private int _ret;
		private int _tst;
		private readonly Func<bool[]> _readJoyData;
		private readonly Func<bool[]> _readKeyData;

		public JoystickKeyboardPort(Func<bool[]> readJoyData, Func<bool[]> readKeyData)
		{
			_readJoyData = readJoyData;
			_readKeyData = readKeyData;
		}

		private byte GetJoystick1()
		{
			var joyData = _readJoyData();
			var result = 0xE0 |
				(joyData[0] ? 0x00 : 0x01) |
				(joyData[1] ? 0x00 : 0x02) |
				(joyData[2] ? 0x00 : 0x04) |
				(joyData[3] ? 0x00 : 0x08) |
				(joyData[4] ? 0x00 : 0x10);
			return unchecked((byte) result);
		}

		private byte GetJoystick2()
		{
			var joyData = _readJoyData();
			var result = 0xE0 |
				(joyData[5] ? 0x00 : 0x01) |
				(joyData[6] ? 0x00 : 0x02) |
				(joyData[7] ? 0x00 : 0x04) |
				(joyData[8] ? 0x00 : 0x08) |
				(joyData[9] ? 0x00 : 0x10);
			return unchecked((byte) result);
		}

		private byte GetKeyboardRows(int activeColumns)
		{
			var keyData = _readKeyData();
			var result = 0xFF;
			for (var r = 0; r < 8; r++)
			{
				if ((activeColumns & 0x1) == 0)
				{
					var i = r << 3;
					for (var c = 0; c < 8; c++)
					{
						if (keyData[i++])
						{
							result &= ~(1 << c);
						}
					}
				}

				activeColumns >>= 1;
			}

			return unchecked((byte)result);
		}

		private int GetKeyboardColumns(int activeRows)
		{
			var keyData = _readKeyData();
			var result = 0xFF;
			for (var c = 0; c < 8; c++)
			{
				if ((activeRows & 0x1) == 0)
				{
					var i = c;
					for (var r = 0; r < 8; r++)
					{
						if (keyData[i])
						{
							result &= ~(1 << r);
						}

						i += 0x8;
					}
				}

				activeRows >>= 1;
			}

			return result;
		}

		public byte ReadPra(byte pra, byte ddra, byte prb, byte ddrb)
		{
			_ret = (pra | ~ddra) & 0xFF;
			_tst = (prb | ~ddrb) & GetJoystick1();
			_ret &= GetKeyboardColumns(_tst);
			return unchecked((byte)(_ret & GetJoystick2()));
		}

		public byte ReadPrb(byte pra, byte ddra, byte prb, byte ddrb)
		{
			_ret = ~ddrb & 0xFF;
			_tst = (pra | ~ddra) & GetJoystick2();
			_ret &= GetKeyboardRows(_tst);
			return unchecked((byte) ((_ret | (prb & ddrb)) & GetJoystick1()));
		}
	}

	private sealed class IecPort : IPort
	{
		private readonly Func<byte> _readIec;
		private readonly Func<byte> _readUserPort;

		public IecPort(Func<byte> readIec, Func<byte> readUserPort)
		{
			_readIec = readIec;
			_readUserPort = readUserPort;
		}

		public byte ReadPra(byte pra, byte ddra, byte prb, byte ddrb)
		{
			return unchecked((byte)((pra & ddra) | (~ddra & _readIec())));
		}

		public byte ReadPrb(byte pra, byte ddra, byte prb, byte ddrb)
		{
			return unchecked((byte)(prb | ~ddrb | (~ddrb & _readUserPort())));
		}
	}
}