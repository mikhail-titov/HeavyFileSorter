The solution is based on .NET 8 and C# 13.

**TestDataGenerator**

To generate test data please use TestDataGenerator.
usage:

TestDataGenerator.exe

      --count | -c <countOfRecords>
      --output | -o <outputFile>
      Example: .\TestDataGenerator.exe -c 300 -o testData.txt

**HeavyFileSorter**

usage: 

      --input | -i <inputFile>
  		Description: Specifies the path to the input file that contains the data to be processed.
		Example:  .\HeavyFileSorter.exe  --input inputFile.txt or -i inputFile.txt
   
     --output | -o <outputFile>
		Description: Specifies the path to the output file where the processed data will be saved.
		Example: .\HeavyFileSorter.exe --output outputFile.txt or -o outputFile.txt
   
     --check | -c <fileNameToCheck>
		Description: Specifies the path to a file that should be checked to see if it is sorted.
		Example: .\HeavyFileSorter.exe --check fileToCheck.txt or -c fileToCheck.txt
   
     --help | -h | help
		Description: Displays usage information for the program.
		Example: .\HeavyFileSorter.exe help

HeavyFileSorter is designed to efficiently sort and manage large datasets by breaking them into manageable chunks, processing them concurrently, and ensuring data integrity throughout the process.
Expected execution time is less than 15 sec for 1GB (20M strings) of input data, maximum RAM consumption is up to about 2.5 GB even for heavy files.
Input string shoul contain text with the specified foramt like "123456. Some text".
Where number is signed long integer and each string has length up to 1024 ASCII symbols.
Sorting algorithm is case insensitive.

**General stages of the algorithm:**
1.	Read and Parse Data:

	• Split a large file into smaller chunks with limited size.

	• Sort each chunk individually.

	• Merge the sorted chunks back into a single, sorted output file.

3. Check Data Integrity:

	• Verify if a file is sorted correctly.

4.	Manage File Operations:

	• Serialize and deserialize Entity objects to and from JSON files.

	• Append entities to an existing file.

5.	Optimize Performance:

	• Calculate optimal chunk sizes for processing based on system resources.

	• Use concurrent operations to improve performance during file reading, writing, and merging.

6.	Handle Large Files:

	• Ensure there is enough disk space before processing.

	• Find precise chunk borders to avoid splitting entities across chunks.
