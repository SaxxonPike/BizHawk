namespace BizHawk.Emulation.Cores.Computers.Commodore64.MOS
{
	public sealed partial class Via
	{
		public byte Peek(ushort addr)
		{
			return ReadRegister(addr);
		}

		public void Poke(ushort addr, byte val)
		{
			WriteRegister(addr, val);
		}

		public byte Read(ushort addr)
		{
			addr &= 0xF;
			switch (addr)
			{
				case 0x0:
					if (_pcrCb2Control != PCR_CONTROL_INDEPENDENT_INTERRUPT_INPUT_NEGATIVE_EDGE && _pcrCb2Control != PCR_CONTROL_INDEPENDENT_INTERRUPT_INPUT_POSITIVE_EDGE)
						_ifr &= 0xE7;
					if (_acrPbLatchEnable)
						return _pbLatch;
					break;
				case 0x1:
					if (_pcrCa2Control != PCR_CONTROL_INDEPENDENT_INTERRUPT_INPUT_NEGATIVE_EDGE && _pcrCa2Control != PCR_CONTROL_INDEPENDENT_INTERRUPT_INPUT_POSITIVE_EDGE)
						_ifr &= 0xFC;
					if (_acrPaLatchEnable)
						return _paLatch;
					break;
				case 0x4:
					_ifr &= 0xBF;
					break;
				case 0x8:
					_ifr &= 0xDF;
					break;
				case 0xA:
					_ifr &= 0xFB;
					_srCount = 8;
					break;
				case 0xF:
					if (_acrPaLatchEnable)
					{
						return _paLatch;
					}
					break;
			}

			return ReadRegister(addr);
		}

		private byte ReadRegister(ushort addr)
		{
			switch (addr)
			{
				case 0x0:
					return _port.ReadPrb(_prb, _ddrb);
				case 0x1:
				case 0xF:
					return _port.ReadExternalPra();
				case 0x2:
					return _ddrb;
				case 0x3:
					return _ddra;
				case 0x4:
					return unchecked((byte)_t1C);
				case 0x5:
					return unchecked((byte)(_t1C >> 8));
				case 0x6:
					return unchecked((byte)_t1L);
				case 0x7:
					return unchecked((byte) (_t1L >> 8));
				case 0x8:
					return unchecked((byte)_t2C);
				case 0x9:
					return unchecked((byte) (_t2C >> 8));
				case 0xA:
					return _sr;
				case 0xB:
					return _acr;
				case 0xC:
					return _pcr;
				case 0xD:
					return _ifr;
				case 0xE:
					return unchecked((byte) (_ier | 0x80));
			}

			return 0xFF;
		}

		public void Write(ushort addr, byte val)
		{
			addr &= 0xF;
			switch (addr)
			{
				case 0x0:
					if (_pcrCb2Control != PCR_CONTROL_INDEPENDENT_INTERRUPT_INPUT_NEGATIVE_EDGE && _pcrCb2Control != PCR_CONTROL_INDEPENDENT_INTERRUPT_INPUT_POSITIVE_EDGE)
						_ifr &= 0xE7;
					if (_pcrCb2Control == PCR_CONTROL_PULSE_OUTPUT)
						_handshakeCb2NextClock = true;
					WriteRegister(addr, val);
					break;
				case 0x1:
					if (_pcrCa2Control != PCR_CONTROL_INDEPENDENT_INTERRUPT_INPUT_NEGATIVE_EDGE && _pcrCa2Control != PCR_CONTROL_INDEPENDENT_INTERRUPT_INPUT_POSITIVE_EDGE)
						_ifr &= 0xFC;
					if (_pcrCa2Control == PCR_CONTROL_PULSE_OUTPUT)
						_handshakeCa2NextClock = true;
					WriteRegister(addr, val);
					break;
				case 0x4:
				case 0x6:
					_t1L = (_t1L & 0xFF00) | (val & 0xFF);
					break;
				case 0x5:
					_t1L = (_t1L & 0xFF) | ((val & 0xFF) << 8);
					_ifr &= 0xBF;
					_t1C = _t1L;
					_t1CLoaded = true;
					_t1Delayed = 1;
					_resetPb7NextClock = _acrT1Control == ACR_T1_CONTROL_INTERRUPT_ON_LOAD_AND_PULSE_PB7;
					break;
				case 0x7:
					_t1L = (_t1L & 0xFF) | ((val & 0xFF) << 8);
					_ifr &= 0xBF;
					break;
				case 0x8:
					_t2L = (_t2L & 0xFF00) | (val & 0xFF);
					break;
				case 0x9:
					_t2L = (_t2L & 0xFF) | ((val & 0xFF) << 8);
					_ifr &= 0xDF;
					if (_acrT2Control == ACR_T2_CONTROL_TIMED)
					{
						_t2C = _t2L;
						_t2CLoaded = true;
					}

					_t2Delayed = 1;
					break;
				case 0xA:
					_ifr &= 0xFB;
					_srCount = 8;
					WriteRegister(addr, val);
					break;
				case 0xD:
					_ifr &= unchecked((byte)(~val));
					break;
				case 0xE:
					if ((val & 0x80) != 0)
						_ier |= unchecked((byte) (val & 0x7F));
					else
						_ier &= unchecked((byte) ~val);
					break;
				default:
					WriteRegister(addr, val);
					break;
			}
		}

		private void WriteRegister(ushort addr, byte val)
		{
			addr &= 0xF;
			switch (addr)
			{
				case 0x0:
					_prb = val;
					break;
				case 0x1:
				case 0xF:
					_pra = val;
					break;
				case 0x2:
					_ddrb = val;
					break;
				case 0x3:
					_ddra = val;
					break;
				case 0x4:
					_t1C = (_t1C & 0xFF00) | val;
					break;
				case 0x5:
					_t1C = (_t1C & 0xFF) | (val << 8);
					break;
				case 0x6:
					_t1L = (_t1L & 0xFF00) | val;
					break;
				case 0x7:
					_t1L = (_t1L & 0xFF) | (val << 8);
					break;
				case 0x8:
					_t2C = (_t2C & 0xFF00) | val;
					break;
				case 0x9:
					_t2C = (_t2C & 0xFF) | (val << 8);
					break;
				case 0xA:
					_sr = val;
					break;
				case 0xB:
					_acr = val;
					_acrPaLatchEnable = (val & 0x01) != 0;
					_acrPbLatchEnable = (val & 0x02) != 0;
					_acrSrControl = (val & 0x1C);
					_acrT2Control = (val & 0x20);
					_acrT1Control = (val & 0xC0);
					break;
				case 0xC:
					_pcr = val;
					_pcrCa1IntControl = _pcr & 0x01;
					_pcrCa2Control = _pcr & 0x0E;
					_pcrCb1IntControl = (_pcr & 0x10) >> 4;
					_pcrCb2Control = (_pcr & 0xE0) >> 4;
					break;
				case 0xD:
					_ifr = val;
					break;
				case 0xE:
					_ier = val;
					break;
			}
		}

		public byte DdrA => _ddra;

		public byte DdrB => _ddrb;

		public byte PrA => _pra;

		public byte PrB => _prb;

		public byte EffectivePrA => unchecked((byte) (_pra | ~_ddra));

		public byte EffectivePrB => unchecked((byte) (_prb | ~_ddrb));

		public byte ActualPrA => _acrPaLatchEnable ? _paLatch : _port.ReadPra(_pra, _ddra);

		public byte ActualPrB => _acrPbLatchEnable ? _pbLatch : _port.ReadPrb(_prb, _ddrb);
	}
}
