using System.Text;

namespace BizHawk.Common.Extensions;

public static class StringBuilderExtensions
{
	private const string HEX_ALPHABET = "0123456789ABCDEF";

	public static void AppendHex(this StringBuilder sb, long value, int count)
	{
		for (var shift = (count - 1) * 4; shift >= 0; shift -= 4)
		{
			sb.Append(HEX_ALPHABET[(int) ((value >> shift) & 0xF)]);
		}
	}
}