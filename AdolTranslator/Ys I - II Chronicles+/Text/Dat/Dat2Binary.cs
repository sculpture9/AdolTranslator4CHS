using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Yarhl.FileFormat;
using Yarhl.IO;

namespace AdolTranslator.Text.Dat
{
    public class Dat2Binary : IConverter<Dat, BinaryFormat>
    {
        private DataWriter writer;
        private Dat dat;
        public static Dictionary<string, string> Map = new Dictionary<string, string>();

        public static string dictionaryDir =
            $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}{Path.DirectorySeparatorChar}";

        public BinaryFormat Convert(Dat source)
        {
            writer = new DataWriter(new DataStream());
            dat = source;

            if (File.Exists(dictionaryDir + "text.ini"))
                GenerateDictionary();

            FillHeader();
            WriteData();
            UpdateHeader();

            return new BinaryFormat(writer.Stream);
        }

        private void FillHeader()
        {
            writer.Write(dat.Count);
            writer.WriteTimes(0, (dat.Count + 1) * 4);
        }

        private void WriteData()
        {
            var currentPosition = (int) writer.Stream.Position;

            foreach (var text in dat.TextList)
            {
                if (text == "<!empty>")
                {
                    dat.SizesList.Add(-1);
                    continue;
                }

                //var bytes = Binary2Dat.Sjis.GetBytes(ReplaceChars(text) + "\0");
                var bytes = GetCustomSjis(text);
                var encrypted = Binary2Dat.XorEncryption(bytes);
                writer.Write(encrypted);
                dat.SizesList.Add(encrypted.Length);
            }

            dat.DataSize = (int)writer.Stream.Position - currentPosition;
        }

        private void UpdateHeader()
        {
            writer.Stream.Position = 4;
            writer.Write(dat.DataSize);
            foreach (var size in dat.SizesList)
            {
                writer.Write(size);
            }
        }

        public static void GenerateDictionary(string anotherDic = "text.ini")
        {
            var textFile = File.ReadAllLines(dictionaryDir + anotherDic);
            Map.Clear();
            /*            foreach (var s in textFile)
                        {
                            var splitted = s.Split(' ');
                            var utf = Encoding.GetEncoding(1252).GetString(GetBytesFromString(splitted[0]));
                            var sjis = Binary2Dat.Sjis.GetString(GetBytesFromString(splitted[1]));
                            Map.Add(utf, sjis);
                        }*/
            for (int i = 0; i < textFile.Length; i++)
            {
                var s = textFile[i];
                var splitted = s.Split(' ');
                var utf = splitted[0];
                var sjis = splitted[1];
                if (Map.ContainsKey(utf))
                {
                    var utfChar = Encoding.UTF32.GetString(BitConverter.GetBytes(int.Parse(utf)));
                    Console.WriteLine($"the key: \"{utfChar}\", unicode: {utf} is already exists! Now override!");
                    Map.Remove(utf);
                    Map.Add(utf, sjis);
                    continue;
                }
                Map.Add(utf, sjis);
            }
        }

        public static string ReplaceChars(string ori)
        {
            return Map.Aggregate(ori, (current, key) => current.Replace(key.Key, key.Value));
        }

        private static byte[] GetBytesFromString(string intText)
        {
            var list = new List<byte>();
            list.AddRange(BitConverter.GetBytes(System.Convert.ToInt32(intText)));
            do
            {
                list.Remove(0);
            } while (list.Contains(0));
            list.Reverse();
            return list.ToArray();
        }

        private static byte[] GetCustomSjis(string text)
        {
            var charArray = text.ToCharArray();
            List<byte> result = new List<byte>();

            for (int i = 0; i < charArray.Length; i++)
            {
                //get the mapping of utf32 char code in the game
                var charU32Bytes = Encoding.UTF32.GetBytes(charArray, i, 1);
                var charCodeStr = BitConverter.ToInt32(charU32Bytes, 0).ToString();
                if (Map.ContainsKey(charCodeStr))
                {
                    charCodeStr = Map[charCodeStr];
                }
                //if use the original char, get utf8 char code
                else
                {
                    var kkk = charCodeStr;
                    string keyChar = charArray[i].ToString();
                    charCodeStr = GetUTF8CodeFromChar(keyChar).ToString();
                    if (!string.Equals(kkk, charCodeStr))
                    {
                        Console.WriteLine("┌----------------------------------");
                        Console.WriteLine($" The unknown char :[{"\"" + keyChar + "\""}] either in \"text.ini\" nor in game");
                        Console.WriteLine($" char's utf32 code: {kkk}");
                        Console.WriteLine($" char's utf8  code: {charCodeStr}");
                        Console.WriteLine("└----------------------------------");
                    }
                }
                //get custom shiftjis bytes
                byte[] sjisByte = GetBytesFromString(charCodeStr);
                for (int j = 0; j < sjisByte.Length; j++)
                {
                    result.Add(sjisByte[j]);
                }
            }
            //add "\0" from Shift-jis at the end of text
            result.Add((byte)0);
            return result.ToArray();
        }
        private static int GetUTF8CodeFromChar(string charStr)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(charStr);
            int result = -1;
            string temp;
            switch (bytes.Length)
            {
                case 1:
                    temp = System.Convert.ToString(bytes[0], 10);
                    result = System.Convert.ToInt32(temp);
                    break;
                case 2:
                    temp = System.Convert.ToString(bytes[0], 2).PadLeft(8, '0')
                        + System.Convert.ToString(bytes[1], 2).PadLeft(8, '0');
                    result = System.Convert.ToInt32(temp, 2);
                    break;
                case 3:
                    temp = System.Convert.ToString(bytes[0], 2).PadLeft(8, '0')
                        + System.Convert.ToString(bytes[1], 2).PadLeft(8, '0')
                        + System.Convert.ToString(bytes[2], 2).PadLeft(8, '0');
                    result = System.Convert.ToInt32(temp, 2);
                    break;
                case 4:
                    result = BitConverter.ToInt32(bytes);
                    break;
            }
            return result;
        }
    }
}

