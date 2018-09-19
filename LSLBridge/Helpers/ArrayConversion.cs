using System;

namespace LSLBridge.Helpers
{
    public static class ArrayConversion
    {
        public static T[] To1DArray<T>(this T[,] array2D)
        {
            if (array2D == null) throw new ArgumentNullException("array2D");
            int rowCount = array2D.GetLength(0), colCount = array2D.GetLength(1);
            T[] array1D = new T[rowCount * colCount];
            for (int colIndex = 0; colIndex < colCount; colIndex++)
            {
                for(int rowIndex = 0; rowIndex < rowCount; rowIndex++)
                {
                    array1D[(colIndex * rowCount) + rowIndex] = array2D[rowIndex, colIndex];
                }
            }
            return array1D;
        }

        public static T[,] To2DArray<T>(this T[] array1D, int rowCount, int colCount)
        {
            if (array1D == null) throw new ArgumentNullException("array1D");
            if (rowCount * colCount != array1D.LongLength) throw new ArgumentException("rowCount - colCount combination invalid - does not equal length of array.");
            T[,] array2D = new T[rowCount, colCount];
            for (int colIndex = 0; colIndex < colCount; colIndex++)
            {
                for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
                {
                    array2D[rowIndex, colIndex] = array1D[(colIndex * rowCount) + rowIndex];
                }
            }
            return array2D;
        }
    }
}
