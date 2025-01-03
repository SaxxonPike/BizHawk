using BizHawk.Emulation.Common;

namespace BizHawk.Emulation.Cores.Computers.Commodore64.Cartridge;

/// <summary>
/// AMD flash chip used for EasyFlash emulation.
/// </summary>
public class Am29F040
{
	// Source:
	// https://datasheet.octopart.com/AM29F040-90JC-AMD-datasheet-18512040.pdf
	//
	// Signals (while busy)
	// ----
	// DQ7      Data polling
	// DQ6      Toggle bit
	// DQ5      Exceeded timing limits
	// DQ3      Sector erase timer
	
	// Operations
	// ----------
	//
	// "Read/Reset":
	// $5555 <- $AA
	// $2AAA <- $55
	// $5555 <- $F0
	// addr  -> data
	//
	// "Autoselect": (used for hardware detection)
	// $5555 <- $AA
	// $2AAA <- $55
	// $5555 <- $90
	// $0000 -> #$01
	// $0001 -> #$A4
	//
	// "Byte Program": (modify rom)
	// $5555 <- $AA
	// $2AAA <- $55
	// $5555 <- $A0
	// addr  <- data
	//
	// "Chip Erase": (reset entire chip to 1)
	// $5555 <- $AA
	// $2AAA <- $55
	// $5555 <- $80
	// $5555 <- $AA
	// $2AAA <- $55
	// $5555 <- $10
	// 
	// "Sector Erase": (reset 64k sector to 1)
	// $5555 <- $AA
	// $2AAA <- $55
	// $5555 <- $80
	// $5555 <- $AA
	// $2AAA <- $55
	// addr  <- $30
	// [only accepts "Erase Suspend"/"Erase Resume" commands until complete]
	//
	// The API code runs on the C64 itself at ~1mhz and the flash device
	// runs at a much higher clock rate. For some time, emulators implemented these as
	// instantaneous operations. A big TODO is determine how many C64 clock cycles
	// these operations actually take - the flash chip might be fast enough that this
	// is ultimately moot. The datasheet claims access times down to "55ns" which
	// equates to about 18mhz, far in excess of the system it is connected to. AMD
	// explicitly does not provide exact timings of the erase functions.
	//
	// Another TODO (probably not relevant to *any* released EasyFlash cartridges)
	// would be to implement the "erase suspend/resume" functionality. But for now we
	// will just assume that the operations complete so fast that the API does not need
	// to sit in a wait loop.

	public const int Command0Address = 0x5555;
	public const int Command1Address = 0x2AAA;
	public const int Command0Signal = 0xAA;
	public const int Command1Signal = 0x55;

	public const int ImageSize = 1 << 19;
	public const int ImageMask = ImageSize - 1;
	private const int SectorSize = 1 << 16;
	private const int SectorMask = SectorSize - 1;

	private const int ReadResetCommand = 0xF0;
	private const int AutoSelectCommand = 0x90;
	private const int ByteProgramCommand = 0xA0;
	private const int ChipEraseCommand = 0x10;
	private const int SectorEraseCommand = 0x30;
	private const int ErasePrefixCommand = 0x80;

	private enum Sequence
	{
		None,
		Start,
		Complete,
		Command
	}
	
	private enum Mode
	{
		Read,
		Erase,
		AutoSelect,
		Write
	}
	
	private int _status;
	private bool _statusReady;
	private int[] _data;
	private Mode _mode;
	private Sequence _sequence;

	public MemoryDomain CreateMemoryDomain(string name) =>
		new MemoryDomainDelegate(
			name: name,
			size: ImageSize,
			endian: MemoryDomain.Endian.Little,
			peek: a => unchecked((byte) _data[a]),
			poke: (a, d) => _data[a] = d,
			wordSize: 1
		);

	public Span<int> Data =>
		_data.AsSpan();

	public int Peek(int addr) =>
		_data[addr & ImageMask] & 0xFF;

	public int Poke(int addr) =>
		_data[addr & ImageMask] = addr & 0xFF;
	
	public int Read(int addr)
	{
		int data;

		switch (_statusReady, _mode, addr)
		{
			case (false, _, _):
			{
				data = _data[addr & ImageMask];
				break;
			}
			case (_, Mode.AutoSelect, 0x0000):
			{
				data = 0x01;
				break;
			}
			case (_, Mode.AutoSelect, 0x0001):
			{
				data = 0xA4;
				break;
			}
			default:
			{
				data = _status;
				_statusReady = false;
				break;
			}
		}

		_status ^= 0x40;
		return data;
	}

	public void Write(int addr, int data)
	{
		switch (_mode, _sequence, addr, data)
		{
			case (Mode.Write, _, _, _):
			{
				_data[addr & ImageMask] = data & 0xFF;
				_statusReady = true;
				_status = (data & 0x80) | (_status & 0x40) | 0x08;
				_mode = Mode.Read;
				_sequence = Sequence.None;
				break;
			}
			case (_, _, 0x5555, 0xAA):
			{
				_statusReady = false;
				_sequence = Sequence.Start;
				break;
			}
			case (_, Sequence.Start, 0x2AAA, 0x55):
			{
				_sequence = Sequence.Complete;
				break;
			}
			case (_, Sequence.Complete, 0x5555, 0x80):
			{
				_sequence = Sequence.None;
				_mode = Mode.Erase;
				break;
			}
			case (Mode.Erase, Sequence.Complete, 0x5555, 0x10):
			{
				_sequence = Sequence.None;
				_data.AsSpan().Fill(0xFF);
				_mode = Mode.Read;
				break;
			}
			case (Mode.Erase, Sequence.Complete, _, 0x30):
			{
				_sequence = Sequence.None;
				_data.AsSpan(addr & ~0xFFFF, 0x10000).Fill(0xFF);
				_mode = Mode.Read;
				_statusReady = true;
				break;
			}
			case (Mode.Read, Sequence.Complete, 0x5555, 0x90):
			{
				_sequence = Sequence.None;
				_statusReady = true;
				_mode = Mode.AutoSelect;
				_status = 0;
				break;
			}
			case (Mode.Read, Sequence.Complete, 0x5555, 0xA0):
			{
				_mode = Mode.Write;
				break;
			}
			case (_, _, _, 0xF0):
			{
				_sequence = Sequence.None;
				_mode = Mode.Read;
				_statusReady = false;
				break;
			}
		}
	}
}