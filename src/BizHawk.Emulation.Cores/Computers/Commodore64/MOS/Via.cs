using System.Runtime.CompilerServices;
using BizHawk.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.MOS
{
	public sealed partial class Via
	{
		private const int PB6_MASK = 1 << 6;
		private const int PB6_TAP = 0b11 << 6;
		private const int PB6_NEGATIVE_EDGE = 0b10 << 6;

		private const int IRQ_CA2 = 1 << 0;
		private const int IRQ_CA1 = 1 << 1;
		private const int IRQ_SR = 1 << 2;
		private const int IRQ_CB2 = 1 << 3;
		private const int IRQ_CB1 = 1 << 4;
		private const int IRQ_T2 = 1 << 5;
		private const int IRQ_T1 = 1 << 6;
		private const int IRQ_MASK = (1 << 7) - 1;

		private const int ACR_LATCH_PA = 1 << 0;
		private const int ACR_LATCH_PB = 1 << 1;
		private const int ACR_SR = 0b111 << 2;
		private const int ACR_SR_CLOCK = 0b011 << 2;
		private const int ACR_SR_USE_T2 = 0b101 << 2;
		private const int ACR_SR_OUT = 0b100 << 2;
		private const int ACR_SR_CLOCK_T2 = 0b001 << 2;
		private const int ACR_SR_CLOCK_PHI2 = 0b010 << 2;
		private const int ACR_SR_CLOCK_EXT = 0b011 << 2;
		private const int ACR_T2_COUNT_PB6 = 1 << 5;
		private const int ACR_T1_FREERUN = 1 << 6;
		private const int ACR_T1_PB7_OUT = 1 << 7;

		private const int PCR_CA1_POLARITY = 1 << 0;
		private const int PCR_CA2_MODE = 0b111 << 1;
		private const int PCR_CA2_ACK = 0b001 << 1;
		private const int PCR_CA2_POLARITY = 0b010 << 1;
		private const int PCR_CA2_OUT = 0b100 << 1;
		private const int PCR_CA2_MODE_HANDSHAKE = 0b100 << 1;
		private const int PCR_CA2_MODE_PULSE = 0b101 << 1;
		private const int PCR_CA2_MODE_LOW = 0b110 << 1;
		private const int PCR_CA2_MODE_HIGH = 0b111 << 1;
		private const int PCR_CB1_POLARITY = 1 << 4;
		private const int PCR_CB2_MODE = 0b111 << 5;
		private const int PCR_CB2_ACK = 0b001 << 5;
		private const int PCR_CB2_POLARITY = 0b010 << 5;
		private const int PCR_CB2_OUT = 0b100 << 5;
		private const int PCR_CB2_MODE_HANDSHAKE = 0b100 << 5;
		private const int PCR_CB2_MODE_PULSE = 0b101 << 5;
		private const int PCR_CB2_MODE_LOW = 0b110 << 5;
		private const int PCR_CB2_MODE_HIGH = 0b111 << 5;

		private const int EDGE_NEGATIVE = 0b10;
		private const int EDGE_POSITIVE = 0b01;
		private const int EDGE_MASK = 0b11;

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
		private int _lastAddr;
		private readonly IPort _port;

		private int _ira;
		private int _irb;
		private int _srCount;

		private bool _ca2Handshake;
		private bool _cb2Handshake;

		public bool Ca2 => _ca2Out;
		public bool Cb1 => _cb1Out;
		public bool Cb2 => _cb2Out;

		private bool _ca2Out;
		private bool _cb1Out;
		private bool _cb2Out;
		private bool _srOn;
		private bool _srDir;
		private int _srBuffer;

		private int _nextIrq;
		private bool _t1Reload;
		private bool _t1IrqAllowed;
		private bool _t2IrqAllowed;
		private int _ca1Buffer;
		private int _ca2Buffer;
		private int _cb1Buffer;
		private int _cb2Buffer;
		private int _pb6Buffer;

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
			_nextIrq = 0;
			_t1Out = true;
			_ca2Handshake = true;
			_cb2Handshake = true;
			_t1IrqAllowed = false;
			_srBuffer = ~0;
			_ca1Buffer = ~0;
			_ca2Buffer = ~0;
			_cb1Buffer = ~0;
			_cb2Buffer = ~0;
			_pb6Buffer = ~0;
			_t1Reload = false;
			_t2IrqAllowed = false;
			_irq = false;
		}

		/// <summary>
		/// Execute one full phase of VIA logic.
		/// </summary>
		public void ExecutePhase()
		{
			// Generated interrupts are available externally on the following clock.
			var thisIrq = _nextIrq;
			_ifr |= _nextIrq & IRQ_MASK;
			_irq = (_ier & _ifr & IRQ_MASK) != 0;
			_nextIrq = 0;

			// CA1, CA2, CB1, and CB2 are all buffered for edge detection each clock.
			// The state of each pin is shifted in at bit 0 each clock.
			_ca1Buffer = (_ca1Buffer << 1) | (ReadCa1() ? 1 : 0);
			_ca2Buffer = (_ca2Buffer << 1) | (ReadCa2() ? 1 : 0);
			_cb1Buffer = (_cb1Buffer << 1) | (ReadCb1() ? 1 : 0);
			_cb2Buffer = (_cb2Buffer << 1) | (ReadCb2() ? 1 : 0);

			// Handshake on CA2 occurs after an edge transition on CA1.
			if ((thisIrq & IRQ_CA1) != 0)
			{
				_ca2Handshake = false;
			}

			// Handshake on CB2 occurs after an edge transition on CB1.
			if ((thisIrq & IRQ_CB1) != 0)
			{
				_cb2Handshake = false;
			}

			// In output mode, CA2 has one of four level sources.
			// In input mode, CA2 will reflect the input buffer.
			_ca2Out = (_pcr & PCR_CA2_MODE) switch
			{
				PCR_CA2_MODE_HANDSHAKE => _ca2Handshake,
				PCR_CA2_MODE_PULSE => _lastAddr != 0x1,
				PCR_CA2_MODE_LOW => false,
				_ => true
			};

			// In output mode, CB2 has one of four level sources.
			// In input mode, CB2 will reflect the input buffer.
			_cb2Out = (_pcr & PCR_CB2_MODE) switch
			{
				PCR_CB2_MODE_HANDSHAKE => _cb2Handshake,
				PCR_CB2_MODE_PULSE => _lastAddr != 0x0,
				PCR_CB2_MODE_LOW => false,
				_ => true,
			};

			// PB6 edge detection occurs on the external pin independently of
			// the ORB and DDRB registers.
			var pbIn = _port.ReadExternalPrb();
			_pb6Buffer = (_pb6Buffer << 1) | (pbIn & PB6_MASK);

			// IRA is loaded when:
			// - PA latch is disabled
			// - a handshake is triggered by transition on CA1
			if ((_acr & ACR_LATCH_PA) == 0 ||
				(_ca2Handshake && (thisIrq & IRQ_CA1) != 0))
			{
				_ira = _port.ReadExternalPra();
			}

			// IRB is loaded when:
			// - PB latch is disabled
			// - a handshake is triggered by transition on CB1
			if ((_acr & ACR_LATCH_PB) == 0 ||
				(_cb2Handshake && (thisIrq & IRQ_CB1) != 0))
			{
				_irb = pbIn;
			}

			// Output of T1 is toggled when an underflow occurs and
			// the timer output is enabled on PB7. The datasheet says
			// this occurs 1.5 cycles after the timer reaches zero.
			if ((thisIrq & IRQ_T1) != 0 && (_acr & ACR_T1_PB7_OUT) != 0)
			{
				_t1Out = !_t1Out;
			}

			// T1 will reload on the cycle following reaching zero.
			if (_t1Reload)
			{
				if (_t1IrqAllowed)
				{
					_nextIrq |= IRQ_T1;
				}

				if ((_acr & ACR_T1_FREERUN) != 0)
				{
					// In free run mode, T1 is reloaded directly
					// from the latch when an underflow occurs.
					// Subsequent interrupts are permitted.
					_t1C = _t1L;
				}
				else
				{
					// In one-shot mode, T1 is not reloaded when
					// an underflow occurs but continues to count
					// down. Subsequent interrupts are not permitted
					// until the high order latch register is written.
					_t1IrqAllowed = false;
				}

				_t1Reload = false;
			}
			else
			{
				// When the counter reaches zero, the following cycle
				// will initiate the reload sequence.
				if (_t1C == 0)
				{
					_t1Reload = true;
				}

				_t1C = (_t1C - 1) & 0xFFFF;
			}

			// T2 either counts negative transitions on PB6 or operates as a one-shot.
			// The pulse output of T2 can be used with shift register operations.
			var srT2 = false;
			if ((_acr & ACR_T2_COUNT_PB6) == 0 || (_pb6Buffer & PB6_TAP) == PB6_NEGATIVE_EDGE)
			{
				// When the counter reaches zero, an interrupt is generated
				// if the counter has been loaded since the last underflow.
				if (_t2C == 0)
				{
					if (_t2IrqAllowed)
					{
						_nextIrq |= IRQ_T2;
						_t2IrqAllowed = false;
					}

				}

				if ((_t2C & 0xFF) == 0 && (_acr & ACR_SR_USE_T2) != 0)
				{
					// When T2 is used to clock shift register operations,
					// only the low order counter is reloaded.
					srT2 = true;
					_t2C = ((_t2C - 1) & ~0xFF) | (_t2L & 0xFF);
				}
				else
				{
					// When T2 is not used to clock shift register operations,
					// both the low and high orders of the counter are reloaded.
					_t2C = (_t2C - 1) & 0xFFFF;
				}
			}

			// Edge detection for CA1, CA2, CB1, CB2.
			if ((_ca1Buffer & EDGE_MASK) == ((_pcr & PCR_CA1_POLARITY) != 0 ? EDGE_POSITIVE : EDGE_NEGATIVE))
			{
				_nextIrq |= IRQ_CA1;
			}

			if ((_ca2Buffer & EDGE_MASK) == ((_pcr & PCR_CA2_POLARITY) != 0 ? EDGE_POSITIVE : EDGE_NEGATIVE))
			{
				_nextIrq |= IRQ_CA2;
			}

			if ((_cb1Buffer & EDGE_MASK) == ((_pcr & PCR_CB1_POLARITY) != 0 ? EDGE_POSITIVE : EDGE_NEGATIVE))
			{
				_nextIrq |= IRQ_CB1;
			}

			if ((_cb2Buffer & EDGE_MASK) == ((_pcr & PCR_CB2_POLARITY) != 0 ? EDGE_POSITIVE : EDGE_NEGATIVE))
			{
				_nextIrq |= IRQ_CB2;
			}

			// Shift register operations.
			var srClock = true;
			var srPulse = false;
			switch (_acr & ACR_SR_CLOCK)
			{
				case 0:
					srPulse = !_srOn 
						? (_acr & ACR_SR_OUT) == 0 && (_srBuffer & EDGE_MASK) == EDGE_POSITIVE
						: srT2;

					srClock = !_srOn
						? (_cb1Buffer & EDGE_MASK) == EDGE_POSITIVE
						: (_srBuffer & EDGE_MASK) == EDGE_POSITIVE;
					break; 
				case ACR_SR_CLOCK_T2:
					srPulse = _srOn && srT2;

					srClock = !_srOn || (_srBuffer & 0b1) == 0;
					break;
				case ACR_SR_CLOCK_PHI2:
					srPulse = _srOn;

					srClock = (_srBuffer & 0b1) == 0;
					break;
				case ACR_SR_CLOCK_EXT:
					srPulse = _srOn && (_srBuffer & EDGE_MASK) == EDGE_POSITIVE;

					srClock = (_cb1Buffer & 0b1) == 1;
					break;
			}

			_srBuffer = (_srBuffer << 1) | (srClock ? 1 : 0);

			if ((_acr & ACR_SR_OUT) != 0)
			{
				if ((_srBuffer & EDGE_MASK) == EDGE_NEGATIVE)
				{
					_sr = ((_sr << 1) & 0xFF) | ((_sr >> 7) & 0x80);
				}
			}
			else
			{
				if ((_srBuffer & EDGE_MASK) == EDGE_POSITIVE)
				{
					_sr = ((_sr << 1) & 0xFF) | ((_cb2Buffer & 0b10) >> 1);
				}
			}

			if (!_srOn && (_srBuffer & EDGE_MASK) == EDGE_POSITIVE && (_acr & ACR_SR) != 0)
			{
				_nextIrq |= IRQ_SR;
			}

			if (_srOn || (_acr & ACR_SR) == 0 || _lastAddr != 0xA)
			{
				if ((_acr & ACR_SR_CLOCK) == 0)
				{
					_srOn = (_acr & ACR_SR_OUT) != 0;
				}
				else if (srClock && srPulse)
				{
					if (_srCount == 0)
					{
						_srOn = false;
					}
					else
					{
						_srCount--;
					}
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
			ser.Sync(nameof(_pb6Buffer), ref _pb6Buffer);
			ser.Sync(nameof(_ca2Handshake), ref _ca2Handshake);
			ser.Sync(nameof(_cb2Handshake), ref _cb2Handshake);
			ser.Sync(nameof(_ca2Out), ref _ca2Out);
			ser.Sync(nameof(_cb1Out), ref _cb1Out);
			ser.Sync(nameof(_cb2Out), ref _cb2Out);
			ser.Sync(nameof(_nextIrq), ref _nextIrq);
			ser.Sync(nameof(_srCount), ref _srCount);
			ser.Sync(nameof(_t1IrqAllowed), ref _t1IrqAllowed);
			ser.Sync(nameof(_t1Out), ref _t1Out);
			ser.Sync(nameof(_srOn), ref _srOn);
			ser.Sync(nameof(_srDir), ref _srDir);
			ser.Sync(nameof(_srBuffer), ref _srBuffer);
			ser.Sync(nameof(_irq), ref _irq);
			ser.Sync(nameof(_t1Reload), ref _t1Reload);
			ser.Sync(nameof(_t2IrqAllowed), ref _t2IrqAllowed);
		}
	}
}
