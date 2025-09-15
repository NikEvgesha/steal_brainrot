using System;


public class CurrencyConverter
{
    private static string[] _namesNum = new string[]
    {
            "",
            "K",
            "M",
            "B",
            "T",
            "a",
            "b",
            "c",
            "d",
            "e",
            "f",
            "g",
            "h",
            "i",
            "k",
            "l",
            "m",
            "n",
            "o",
            "p",
            "q",
            "r",
            "s",
            "t",
            "u",
            "v",
            "w",
            "x",
            "y",
            "z", };
    public static string convertNumToString(double num)
    {
        float multipl = 1000;
        for (int i = 0; i < _namesNum.Length; i++)
        {

            if (num <= 1000)
            {
                if (num >= 100)
                    num = (double)Math.Round(num, 1);
                else
                if (num < 100)
                    num = (double)Math.Round(num, 2);
                else
                if (num < 10)
                    num = (double)Math.Round(num, 3);

                return num.ToString() + _namesNum[i];
            }
            num /= multipl;
        }
        num = (double)Math.Round(num);
        return num.ToString() + _namesNum[_namesNum.Length - 1];
    }

    public static double convertStringToNum(String str)
    {
        return 0f;
    }
}