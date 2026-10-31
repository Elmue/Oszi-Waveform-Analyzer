/*
------------------------------------------------------------
Oscilloscope Waveform Analyzer by ElmüSoft (www.netcult.ch/elmue)
This class is a helper for the OSZI file format.
The OSZI file format is free of any license.
You can use this code even in a closed-source commercial software.
------------------------------------------------------------

NAMING CONVENTIONS which allow to see the type of a variable immediately without having to jump to the variable declaration:
 
     cName  for class    definitions
     tName  for type     definitions
     eName  for enum     definitions
     kName  for "konstruct" (struct) definitions (letter 's' already used for string)
   delName  for delegate definitions

    b_Name  for bool
    c_Name  for Char, also Color
    d_Name  for double
    e_Name  for enum variables
    f_Name  for function delegates, also float
    i_Name  for instances of classes
    k_Name  for "konstructs" (struct) (letter 's' already used for string)
	r_Name  for Rectangle
    s_Name  for strings
    o_Name  for objects
 
   s8_Name  for   signed  8 Bit (sbyte)
  s16_Name  for   signed 16 Bit (short)
  s32_Name  for   signed 32 Bit (int)
  s64_Name  for   signed 64 Bit (long)
   u8_Name  for unsigned  8 Bit (byte)
  u16_Name  for unsigned 16 bit (ushort)
  u32_Name  for unsigned 32 Bit (uint)
  u64_Name  for unsigned 64 Bit (ulong)

  An additional "m" is prefixed for all member variables (e.g. ms_String)
*/ 

using System;
using System.IO;

namespace ExImport
{
    /// <summary>
    /// required to read CAP files from OWON
    /// </summary>
    public class BigEndianReader
    {
        public Stream mi_Stream;

        public Int64 Position
        {
            get { return mi_Stream.Position;  }
            set { mi_Stream.Position = value; }
        }

        public BigEndianReader(Stream i_Stream)
        { 
            mi_Stream = i_Stream;
        }

        public Byte ReadByte()
        {
            int s32_Byte = mi_Stream.ReadByte();
            if (s32_Byte < 0)
                throw new Exception("Insufficient data");
            return (Byte)s32_Byte;
        }

        public Byte[] ReadBytes(int s32_Count, bool b_Reverse = false)
        {
            Byte[] u8_Data = new Byte[s32_Count];
            int s32_Read = mi_Stream.Read(u8_Data, 0, s32_Count);
            if (s32_Read != s32_Count)
                throw new Exception("Insufficient data");

            if (b_Reverse) Array.Reverse(u8_Data);
            return u8_Data;
        }

        public int ReadInt32()
        {
            Byte[] u8_Data = ReadBytes(4, true);
            return BitConverter.ToInt32(u8_Data, 0);
        }

        public float ReadSingle()
        {
            var u8_Data = ReadBytes(4, true);
            return BitConverter.ToSingle(u8_Data, 0);
        }
    }
}
