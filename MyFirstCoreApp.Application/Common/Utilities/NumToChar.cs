namespace MyFirstCoreApp.Application.Common.Utilities
{
    public class NumToChar
    {
        private static string[] yekan = ["صفر", "یک", "دو", "سه", "چهار", "پنج", "شش", "هفت", "هشت", "نه"];
        private static string[] dahgan = ["", "", "بیست", "سی", "چهل", "پنجاه", "شصت", "هفتاد", "هشتاد", "نود"];
        private static string[] dahyek = ["ده", "یازده", "دوازده", "سیزده", "چهارده", "پانزده", "شانزده", "هفده", "هجده", "نوزده"];
        //array[10..19]
        private static string[] sadgan = ["", "یکصد", "دویست", "سیصد", "چهارصد", "پانصد", "ششصد", "هفتصد", "هشتصد", "نهصد"];
        private static string[] basex = ["", "هزار", "میلیون", "میلیارد", "تریلیون"];

        private static string GetNum(int num)
        {
            try
            {
                string s = "";
                int d3, d12;
                d12 = num % 100;
                d3 = num / 100;
                if (d3 != 0)
                    s = sadgan[d3] + " و ";
                if ((d12 >= 10) && (d12 <= 19))
                {
                    s = s + dahyek[d12 - 10];
                }
                else
                {
                    int d2 = d12 / 10;
                    if (d2 != 0)
                        s = s + dahgan[d2] + " و ";
                    int d1 = d12 % 10;
                    if (d1 != 0)
                        s = s + yekan[d1] + " و ";
                    s = s.Substring(0, s.Length - 3);
                }
                ;
                return s;
            }
            catch
            {
                return "";
            }
        }

        public static string NumToStr(string snum)
        {
            try
            {
                string stotal = "";
                if (snum == "0")
                {
                    return yekan[0];
                }
                else
                {
                    snum = snum.PadLeft(((snum.Length - 1) / 3 + 1) * 3, '0');
                    int L = snum.Length / 3 - 1;
                    for (int i = 0; i <= L; i++)
                    {
                        int b = int.Parse(snum.Substring(i * 3, 3));
                        if (b != 0)
                            stotal = stotal + GetNum(b) + " " + basex[L - i] + " و ";
                    }
                    stotal = stotal.Substring(0, stotal.Length - 3);
                }
                return stotal;
            }
            catch (Exception)
            {
                return "";//;snum;           
            }
        }
    }
}