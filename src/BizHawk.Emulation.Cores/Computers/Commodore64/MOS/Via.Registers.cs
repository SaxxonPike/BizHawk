namespace BizHawk.Emulation.Cores.Computers.Commodore64.MOS
{
	public sealed partial class Via
	{
		public int Peek(int addr)
		{
			return ReadRegister(addr & 0xF);
		}

		public void Poke(int addr, int val)
		{
			WriteRegister(addr & 0xF, val);
		}

		public int Read(int addr)
		{
			_lastAddr = addr & 0xF;
			switch (_lastAddr)
			{
				case 0x0:
					_ifr &= ~IRQ_CB1;
					if ((_pcr & PCR_CB2_ACK) == 0)
						_ifr &= ~IRQ_CB2;
					_cb2Handshake = true;
					break;
				case 0x1:
					_ifr &= ~IRQ_CA1;
					if ((_pcr & PCR_CA2_ACK) == 0)
						_ifr &= ~IRQ_CA2;
					_ca2Handshake = true;
					break;
				case 0x4:
					_ifr &= ~IRQ_T1;
					break;
				case 0x8:
					_ifr &= ~IRQ_T2;
					break;
				case 0xA:
					_ifr &= ~IRQ_SR;
					if (!_srOn && (_acr & 0b00011100) != 0)
					{
						_srCount = 7;
						_srOn = true;
					}
					break;
			}

			return ReadRegister(_lastAddr);
		}

		private int ReadRegister(int addr)
		{
			switch (addr)
			{
				case 0x0:
					return (PrB & DdrB) | (_irb & ~DdrB);
				case 0x1:
				case 0xF:
					return _ira;
				case 0x2:
					return _ddrb;
				case 0x3:
					return _ddra;
				case 0x4:
					return _t1C & 0xFF;
				case 0x5:
					return (_t1C >> 8) & 0xFF;
				case 0x6:
					return _t1L & 0xFF;
				case 0x7:
					return (_t1L >> 8) & 0xFF;
				case 0x8:
					return _t2C & 0xFF;
				case 0x9:
					return (_t2C >> 8) & 0xFF;
				case 0xA:
					return _sr;
				case 0xB:
					return _acr;
				case 0xC:
					return _pcr;
				case 0xD:
					return (_ifr & 0x7F) | (_irq ? 0x80 : 0x00);
				case 0xE:
					return _ier;
			}

			return 0xFF;
		}

		public void Write(int addr, int val)
		{
			_lastAddr = addr & 0xF;
			switch (_lastAddr)
			{
				case 0x0:
					_ifr &= 0b11101111;
					if ((_pcr & 0b00100000) == 0)
						_ifr &= 0b11110111;
					WriteRegister(_lastAddr, val);
					break;
				case 0x1:
					_ifr &= 0b11111101;
					if ((_pcr & 0b00000010) == 0)
						_ifr &= 0b11111110;
					WriteRegister(_lastAddr, val);
					break;
				case 0x4:
				case 0x6:
					_t1L = (_t1L & 0xFF00) | (val & 0xFF);
					break;
				case 0x5:
					_t1L = (_t1L & 0xFF) | ((val & 0xFF) << 8);
					_t1C = _t1L;
					_ifr &= ~IRQ_T1;
					_t1Reload = false;
					_t1Out = (_acr & ACR_T1_PB7_OUT) == 0;
					_t1IrqAllowed = true;
					break;
				case 0x7:
					_t1L = (_t1L & 0xFF) | ((val & 0xFF) << 8);
					_ifr &= 0b10111111;
					break;
				case 0x8:
					_t2L = (_t2L & 0xFF00) | (val & 0xFF);
					break;
				case 0x9:
					_t2L = (_t2L & 0xFF) | ((val & 0xFF) << 8);
					_ifr &= 0b11011111;
					_t2IrqAllowed = true;
					break;
				case 0xA:
					_ifr &= 0b11111011;
					_srCount = 8;
					WriteRegister(_lastAddr, val);
					break;
				case 0xD:
					_ifr &= ~(val & 0x7F);
					break;
				case 0xE:
					if ((val & 0x80) != 0)
						_ier |= val & 0x7F;
					else
						_ier &= ~(val & 0x7F);
					break;
				default:
					WriteRegister(_lastAddr, val);
					break;
			}
		}

		private void WriteRegister(int addr, int val)
		{
			switch (addr)
			{
				case 0x0:
					_orb = val & 0xFF;
					break;
				case 0x1:
				case 0xF:
					_ora = val & 0xFF;
					break;
				case 0x2:
					_ddrb = val & 0xFF;
					break;
				case 0x3:
					_ddra = val & 0xFF;
					break;
				case 0x4:
					_t1C = (_t1C & 0xFF00) | (val & 0xFF);
					break;
				case 0x5:
					_t1C = (_t1C & 0xFF) | ((val & 0xFF) << 8);
					break;
				case 0x6:
					_t1L = (_t1L & 0xFF00) | (val & 0xFF);
					break;
				case 0x7:
					_t1L = (_t1L & 0xFF) | ((val & 0xFF) << 8);
					break;
				case 0x8:
					_t2C = (_t2C & 0xFF00) | (val & 0xFF);
					break;
				case 0x9:
					_t2C = (_t2C & 0xFF) | ((val & 0xFF) << 8);
					break;
				case 0xA:
					_sr = val & 0xFF;
					break;
				case 0xB:
					_acr = val & 0xFF;
					break;
				case 0xC:
					_pcr = val & 0xFF;
					break;
				case 0xD:
					_ifr = val & 0x7F;
					break;
				case 0xE:
					_ier = val & 0xFF;
					break;
			}
		}

		public int DdrA => _ddra;

		public int DdrB => _ddrb | (_acr & 0b10000000);

		public int PrA => _ora;

		public int PrB => (_acr & 0b10000000) != 0
			? (_orb & 0x7F) | (_t1Out ? 0x80 : 0x00)
			: _orb;
	}
}
