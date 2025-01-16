using BizHawk.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.MOS
{
	public sealed partial class Via
	{
		private int _ora;
		private int _ddra;
		private int _orb;
		private int _ddrb;
		private int _t1C;
		private int _t1L;
		private int _t2C;
		private int _t2L;
		private bool _t1Out;
		private int _sr;
		private int _acr;
		private int _pcr;
		private int _ifr;
		private int _ier;
		private bool _irq;
		private readonly IPort _port;

		private int _ira;
		private int _irb;
		private int _srCount;

		private bool _ca2Handshake;
		private bool _cb2Handshake;
		private bool _ca2Pulse;
		private bool _cb2Pulse;

		public bool Ca2 => _ca2Out;
		public bool Cb1 => _cb1Out;
		public bool Cb2 => _cb2Out;

		private bool _ca2Out;
		private bool _cb1Out;
		private bool _cb2Out;
		private bool _srOn;
		private bool _srDir;
		private int _srBuffer;

		private int _interruptNextClock;
		private bool _t1Reload;
		private bool _t1IrqAllowed;
		private bool _t2OneShot;
		private int _ca1Buffer;
		private int _ca2Buffer;
		private int _cb1Buffer;
		private int _cb2Buffer;
		private bool _pb6;
		private bool _pb6L;

		public Func<bool> ReadCa1 = () => true;
		public Func<bool> ReadCa2 = () => true;
		public Func<bool> ReadCb1 = () => true;
		public Func<bool> ReadCb2 = () => true;
		
		public Via()
		{
			_port = new DisconnectedPort();
		}

		public Via(Func<int> readPrA, Func<int> readPrB)
		{
			_port = new DriverPort(readPrA, readPrB);
		}

		public Via(Func<bool> readClock, Func<bool> readData, Func<bool> readAtn, int driveNumber)
		{
			_port = new IecPort(readClock, readData, readAtn, driveNumber);
		}

		public bool Irq => _irq;

		public void HardReset()
		{
			_ora = 0;
			_orb = 0;
			_ddra = 0;
			_ddrb = 0;
			_t1C = 0xFFFF;
			_t1L = 0xFFFF;
			_t2C = 0xFFFF;
			_t2L = 0xFFFF;
			_sr = 0xFF;
			_acr = 0;
			_pcr = 0;
			_ifr = 0;
			_irq = false;
			_ier = 0;
			_ira = 0;
			_irb = 0;
			_ca2Out = true;
			_cb1Out = true;
			_cb2Out = true;
			_srCount = 0;
			_interruptNextClock = 0;
			_t1Out = false;
			_ca2Handshake = true;
			_cb2Handshake = true;
			_ca2Pulse = true;
			_cb2Pulse = true;
			_t1IrqAllowed = false;
			_srBuffer = ~0;
			_ca1Buffer = ~0;
			_ca2Buffer = ~0;
			_cb1Buffer = ~0;
			_cb2Buffer = ~0;
			_pb6 = false;
			_pb6L = false;
			_t1Reload = false;
			_t2OneShot = false;
			_irq = false;
		}

		public void ExecutePhase()
		{
			var pbIn = _port.ReadExternalPrb();

			// Interrupt generation
			var interrupts = _interruptNextClock;
			_ifr |= _interruptNextClock & 0x7F;
			_irq = (_ier & _ifr & 0x7F) != 0;
			_interruptNextClock = 0;

			// Port input latches
			if ((_acr & 0b00000001) == 0 || (_ca2Handshake && (interrupts & 0b00000010) != 0))
				_ira = _port.ReadExternalPra(); //_port.ReadPra(0x00, 0x00);
			if ((_acr & 0b00000010) == 0 || (_cb2Handshake && (interrupts & 0b00010000) != 0))
				_irb = pbIn; //_port.ReadPrb(PrB, DdrB);
			
			// Edge detection on CA1, CA2, CB1, CB2
			_ca1Buffer = (_ca1Buffer << 1) | (ReadCa1() ? 1 : 0);
			_ca2Buffer = (_ca2Buffer << 1) | (ReadCa2() ? 1 : 0);
			_cb1Buffer = (_cb1Buffer << 1) | (ReadCb1() ? 1 : 0);
			_cb2Buffer = (_cb2Buffer << 1) | (ReadCb2() ? 1 : 0);

			// Pulse and handshake on CA2
			if ((interrupts & 0b00000010) != 0)
			{
				_ca2Handshake = false;
			}
			
			_ca2Out = (_pcr & 0b00000110) switch
			{
				0b00000000 => _ca2Handshake,
				0b00000010 => _ca2Pulse,
				0b00000100 => false,
				_ => true
			};

			// Pulse and handshake on CB2
			if ((interrupts & 0b00010000) != 0)
			{
				_cb2Handshake = false;
			}
			
			_cb2Out = (_pcr & 0b01100000) switch
			{
				0b00000000 => _cb2Handshake,
				0b00100000 => _cb2Pulse,
				0b01000000 => false,
				_ => true
			};

			// PB6 edge detection
			_pb6L = _pb6;
			_pb6 = (pbIn & 0x40) != 0;
			
			// Timer 1
			if (_t1Reload)
			{
				if (_t1IrqAllowed)
					_interruptNextClock |= 0b01000000;
				_t1C = _t1L & 0xFFFF;
				_t1IrqAllowed &= (_acr & 0b01000000) != 0;
				_t1Reload = false;
				_t1Out = !_t1Out;
			}
			else
			{
				if (_t1C == 0)
					_t1Reload = true;
				_t1C = (_t1C - 1) & 0xFFFF;
			}

			// Timer 2
			var srT2 = false;
			if ((_acr & 0b00100000) == 0 || (!_pb6 && _pb6L))
			{
				if (_t2C == 0)
				{
					if (_t2OneShot)
					{
						_t2OneShot = false;
						_interruptNextClock |= 0b00100000;
					}

				}

				if ((_t2C & 0xFF) == 0 && (_acr & 0b00010100) != 0)
				{
					srT2 = true;
					_t2C = ((_t2C - 1) & ~0xFF) | (_t2L & 0xFF);
				}
				else
				{
					_t2C = (_t2C - 1) & 0xFFFF;
				}
			}
			
			// Process CA1/CA2/CB1/CB2 input interrupts
			if (((_ca1Buffer & 0b01) != 0) ^ ((_ca1Buffer & 0b10) != 0) &&
				((_ca1Buffer & 0b10) != 0) ^ ((_pcr & 0b00000001) != 0))
			{
				_interruptNextClock |= 0b00000010;
			}

			if (((_ca2Buffer & 0b01) != 0) ^ ((_ca2Buffer & 0b10) != 0) &&
				((_ca2Buffer & 0b10) != 0) ^ ((_pcr & 0b00000010) != 0))
			{
				_interruptNextClock |= 0b00000001;
			}

			if (((_cb1Buffer & 0b01) != 0) ^ ((_cb1Buffer & 0b10) != 0) &&
				((_cb1Buffer & 0b10) != 0) ^ ((_pcr & 0b00010000) != 0))
			{
				_interruptNextClock |= 0b00010000;
			}

			if (((_cb2Buffer & 0b01) != 0) ^ ((_cb2Buffer & 0b10) != 0) &&
				((_cb2Buffer & 0b10) != 0) ^ ((_pcr & 0b00100000) != 0))
			{
				_interruptNextClock |= 0b00001000;
			}

			var clk = false;
			bool pulse;

			if (!_srOn)
			{
				pulse = (_acr & 0b00011100) == 0 &&
					(_srBuffer & 0b01) != 0 &&
					(_srBuffer & 0b10) == 0;
				clk = (_acr & 0b00011100) != 0 ||
					(_cb1Buffer & 0b01) != 0;
			}
			else
			{
				pulse = (_acr & 0b00001100) switch
				{
					0b00001000 => true,
					0b00000100 or 0b00000000 => srT2, 
					_ => (_srBuffer & 0b01) != 0 && (_srBuffer & 0b10) == 0
				};

				if ((_acr & 0b00001100) == 0b00001100)
					clk = (_cb1Buffer & 0b01) != 0;
				else
					clk ^= pulse;
			}

			if (_srDir && (_srBuffer & 0b01) != 0 && (_srBuffer & 0b10) == 0)
			{
				_sr = ((_sr << 1) & 0xFF) | ((_sr & 0x80) >> 7);
			}
			else if (!_srDir && (_srBuffer & 0b01) == 0 && (_srBuffer & 0b10) != 0)
			{
				_sr = ((_sr << 1) & 0xFF) | (_cb2Buffer & 0b01);
			}
			
			_srBuffer = (_srBuffer << 1) | (clk ? 1 : 0);

			if ((_srBuffer & 0b01) == 0 && (_srBuffer & 0b10) != 0 && !_srOn && (_acr & 0b00011100) != 0b00000000)
				_interruptNextClock |= 0b00000100;

			if (_srOn || (_acr & 0b00011100) == 0b00000000)
			{
				if ((_acr & 0b00001100) == 0)
				{
					_srOn = _srDir;
				}
				else if (clk && pulse)
				{
					if (_srCount == 0)
						_srOn = false;
					else
						_srCount--;
				}
			}
		}

		public void SyncState(Serializer ser)
		{
			ser.Sync(nameof(_ora), ref _ora);
			ser.Sync(nameof(_ddra), ref _ddra);
			ser.Sync(nameof(_orb), ref _orb);
			ser.Sync(nameof(_ddrb), ref _ddrb);
			ser.Sync(nameof(_t1C), ref _t1C);
			ser.Sync(nameof(_t1L), ref _t1L);
			ser.Sync(nameof(_t2C), ref _t2C);
			ser.Sync(nameof(_t2L), ref _t2L);
			ser.Sync(nameof(_sr), ref _sr);
			ser.Sync(nameof(_acr), ref _acr);
			ser.Sync(nameof(_pcr), ref _pcr);
			ser.Sync(nameof(_ifr), ref _ifr);
			ser.Sync(nameof(_ier), ref _ier);

			ser.BeginSection("Port");
			_port.SyncState(ser);
			ser.EndSection();

			ser.Sync(nameof(_ira), ref _ira);
			ser.Sync(nameof(_irb), ref _irb);
			ser.Sync(nameof(_ca1Buffer), ref _ca1Buffer);
			ser.Sync(nameof(_ca2Buffer), ref _ca2Buffer);
			ser.Sync(nameof(_cb1Buffer), ref _cb1Buffer);
			ser.Sync(nameof(_cb2Buffer), ref _cb2Buffer);
			ser.Sync(nameof(_pb6L), ref _pb6L);
			ser.Sync(nameof(_ca2Handshake), ref _ca2Handshake);
			ser.Sync(nameof(_cb2Handshake), ref _cb2Handshake);
			ser.Sync(nameof(_ca2Pulse), ref _ca2Pulse);
			ser.Sync(nameof(_cb2Pulse), ref _cb2Pulse);
			ser.Sync(nameof(_ca2Out), ref _ca2Out);
			ser.Sync(nameof(_cb1Out), ref _cb1Out);
			ser.Sync(nameof(_cb2Out), ref _cb2Out);
			ser.Sync(nameof(_pb6), ref _pb6);
			ser.Sync(nameof(_interruptNextClock), ref _interruptNextClock);
			ser.Sync(nameof(_srCount), ref _srCount);
			ser.Sync(nameof(_t1IrqAllowed), ref _t1IrqAllowed);
			ser.Sync(nameof(_t1Out), ref _t1Out);
			ser.Sync(nameof(_srOn), ref _srOn);
			ser.Sync(nameof(_srDir), ref _srDir);
			ser.Sync(nameof(_srBuffer), ref _srBuffer);
			ser.Sync(nameof(_irq), ref _irq);
			ser.Sync(nameof(_t1Reload), ref _t1Reload);
			ser.Sync(nameof(_t2OneShot), ref _t2OneShot);
		}
	}
}
