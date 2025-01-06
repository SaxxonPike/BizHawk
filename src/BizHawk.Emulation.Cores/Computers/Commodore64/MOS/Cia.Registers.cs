namespace BizHawk.Emulation.Cores.Computers.Commodore64.MOS;

public sealed partial class Cia
{
	public byte Peek(ushort addr)
	{
		return ReadRegister(unchecked((ushort) (addr & 0xF)));
	}

	public bool ReadIrq()
	{
		return (_icr & 0x80) == 0;
	}

	public byte ReadPortA()
	{
		return unchecked((byte)(_pra | ~_ddra));
	}

	public byte Read(ushort addr)
	{
		addr &= 0xF;
		switch (addr)
		{
			case 0x8:
				_todLatch = false;
				return unchecked((byte)_latch10Ths);
			case 0x9:
				return unchecked((byte)_latchSec);
			case 0xA:
				return unchecked((byte)_latchMin);
			case 0xB:
				_todLatch = true;
				return unchecked((byte)_latchHr);
			case 0xD:
				var icrTemp = _icr;
				_icr = 0;
				return unchecked((byte)icrTemp);
		}

		return ReadRegister(addr);
	}

	private byte ReadRegister(ushort addr)
	{
		switch (addr)
		{
			case 0x0:
				return _port.ReadPra(_pra, _ddra, _prb, _ddrb);
			case 0x1:
				return _port.ReadPrb(_pra, _ddra, _prb, _ddrb);
			case 0x2:
				return _ddra;
			case 0x3:
				return _ddrb;
			case 0x4:
				return unchecked((byte) _ta);
			case 0x5:
				return unchecked((byte) (_ta >> 8));
			case 0x6:
				return unchecked((byte) _tb);
			case 0x7:
				return unchecked((byte) (_tb >> 8));
			case 0x8:
				return _tod10Ths;
			case 0x9:
				return _todSec;
			case 0xA:
				return _todMin;
			case 0xB:
				return _todHr;
			case 0xC:
				return _sdr;
			case 0xD:
				return _icr;
			case 0xE:
				return _cra;
			case 0xF:
				return _crb;
		}

		return 0;
	}

	public void Poke(ushort addr, byte val)
	{
		WriteRegister(addr, val);
	}

	public void Write(ushort addr, byte val)
	{
		addr &= 0xF;
		switch (addr)
		{
			case 0x4:
				_latcha = (_latcha & 0xFF00) | val;
				break;
			case 0x5:
				_latcha = (_latcha & 0xFF) | (val << 8);
				if ((_cra & 0x01) == 0)
				{
					_ta = _latcha;
				}
				break;
			case 0x6:
				_latchb = (_latchb & 0xFF00) | val;
				break;
			case 0x7:
				_latchb = (_latchb & 0xFF) | (val << 8);
				if ((_crb & 0x01) == 0)
				{
					_tb = _latchb;
				}
				break;
			case 0x8:
				if ((_crb & 0x80) != 0)
				{
					_alm10Ths = unchecked((byte) (val & 0xF));
				}
				else
				{
					_tod10Ths = unchecked((byte) (val & 0xF));
				}
				break;
			case 0x9:
				if ((_crb & 0x80) != 0)
				{
					_almSec = unchecked((byte) (val & 0x7F));
				}
				else
				{
					_todSec = unchecked((byte) (val & 0x7F));
				}
				break;
			case 0xA:
				if ((_crb & 0x80) != 0)
				{
					_almMin = unchecked((byte) (val & 0x7F));
				}
				else
				{
					_todMin = unchecked((byte) (val & 0x7F));
				}
				break;
			case 0xB:
				if ((_crb & 0x80) != 0)
				{
					_almHr = unchecked((byte) (val & 0x9F));
				}
				else
				{
					_todHr = unchecked((byte) (val & 0x9F));
				}
				break;
			case 0xC:
				WriteRegister(addr, val);
				// TriggerInterrupt(8); 				
				break;
			case 0xD:
				if ((val & 0x80) != 0)
				{
					_intMask |= (val & 0x7F);
				}
				else
				{
					_intMask &= ~val;
				}
				if ((_icr & _intMask & 0x1F) != 0)
				{
					_icr |= 0x80;
				}
				break;
			case 0xE:
				var oldCra = _cra;
				WriteRegister(addr, val);

				// Toggle output begins high when timer starts.
				if ((_cra & 0x05) == 0x05 && (oldCra & 0x01) == 0)
				{
					_prb |= 0x40;
				}
				break;
			case 0xF:
				var oldCrb = _crb;
				WriteRegister(addr, val);

				// Toggle output begins high when timer starts.
				if ((_crb & 0x05) == 0x05 && (oldCrb & 0x01) == 0)
				{
					_prb |= 0x80;
				}
				break;
			default:
				WriteRegister(addr, val);
				break;
		}
	}

	private void WriteRegister(ushort addr, byte val)
	{
		switch (addr)
		{
			case 0x0:
				_pra = val;
				break;
			case 0x1:
				_prb = val;
				break;
			case 0x2:
				_ddra = val;
				break;
			case 0x3:
				_ddrb = val;
				break;
			case 0x4:
				_latcha = (_latcha & 0xFF00) | val;
				_ta = _latcha;
				break;
			case 0x5:
				_latcha = (_latcha & 0xFF) | (val << 8);
				_ta = _latcha;
				break;
			case 0x6:
				_latchb = (_latchb & 0xFF00) | val;
				_tb = _latchb;
				break;
			case 0x7:
				_latchb = (_latchb & 0xFF) | (val << 8);
				_tb = _latchb;
				break;
			case 0x8:
				_tod10Ths = unchecked((byte)(val & 0xF));
				break;
			case 0x9:
				_todSec = unchecked((byte)(val & 0x7F));
				break;
			case 0xA:
				_todMin = unchecked((byte) (val & 0x7F));
				break;
			case 0xB:
				_todHr = unchecked((byte) (val & 0x9F));
				break;
			case 0xC:
				_sdr = val;
				break;
			case 0xD:
				_intMask = val;
				break;
			case 0xE:
				_hasNewCra = true;
				_newCra = val;
				_taCntPhi2 = (val & 0x20) == 0;
				_taCntCnt = (val & 0x20) == 0x20;
				break;
			case 0xF:
				_hasNewCrb = true;
				_newCrb = val;
				_tbCntPhi2 = (val & 0x60) == 0;
				_tbCntCnt = (val & 0x60) == 0x20;
				_tbCntTa = (val & 0x60) == 0x40;
				_tbCntTaCnt = (val & 0x60) == 0x60;
				break;
		}
	}

	public int DdrA => _ddra;

	public int DdrB => _ddrb;

	public int PrA => _pra;

	public int PrB => _prb;

	public int EffectivePrA => _pra | ~_ddra;

	public int EffectivePrB => _prb | ~_ddrb;
}