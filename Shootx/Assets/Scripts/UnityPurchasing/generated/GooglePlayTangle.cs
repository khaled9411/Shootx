// WARNING: Do not modify! Generated file.

namespace UnityEngine.Purchasing.Security {
    public class GooglePlayTangle
    {
        private static byte[] data = System.Convert.FromBase64String("ynj72Mr3/PPQfLJ8Dff7+/v/+vk9Lp5ExwrNDZd7L7yIOux/2iR1l2oE/PzUcUMe6bY+F/7uoHejWXwOIa+44g4REqrl5WsCUtWMOCqgi8InzNtdXIJ7csW17bUHSQRDlUac8kuXnNb3707hZepkANVJQ3zv/9fDePv1+sp4+/D4ePv7+lQA67QQjDigxw57rnkvIQDeCeftVGp5eHTXye3OgSKkPxZH5xnZYTqu1DPXSKpe/wGp0RFcir/vMaKDPMBW3Y9Yg20ZAu8mySp+jFSF06x5Jg8AoAaswfTTrWD/eWO6D3R5o9K7jLDevz9SuY3UQ6VweaVYjyqS7JV8v//bcgq3k5q3+6VWQJd1bfKVzoqTDUkcWUVSwdN36a4dJ/j5+/r7");
        private static int[] order = new int[] { 0,9,3,4,12,8,9,10,9,12,13,11,13,13,14 };
        private static int key = 250;

        public static readonly bool IsPopulated = true;

        public static byte[] Data() {
        	if (IsPopulated == false)
        		return null;
            return Obfuscator.DeObfuscate(data, order, key);
        }
    }
}
