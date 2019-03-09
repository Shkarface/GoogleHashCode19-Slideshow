#define HASHED_TAGS 
#define DISTINCT_TAGS
#define SORT_TAGS
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoogleHashCode2019_Slideshow
{
    class Program
    {
        #region Inputs
        public static string InputFile => $"{Filename}.{FileExtension}";
        public static string OutputFile => $"{Filename}.out";
        public static readonly string[] InputFiles =
        {
            "a_example",
            "b_lovely_landscapes",
            "c_memorable_moments",
            "d_pet_pictures",
            "e_shiny_selfies"
        };
        public const string FileExtension = "txt";
        #endregion
        public static int VerticalPhotosCount => PhotosCount - HorizontalPhotosCount;
        public static int PhotosCount { get; private set; }
        public static int HorizontalPhotosCount { get; private set; }
        public static int SlidesCount { get; private set; }
        public static int FinalScore { get; private set; }
        public static string Filename { get; private set; }
        public static readonly object ThreadLock = new object();
        public static Stopwatch Stopwatch { get; private set; } = new Stopwatch();
        public static Photo[] Photos;
        public static Slide[] Slides;

        static void Main(string[] args)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            for (int i = 0; i < InputFiles.Length; i++)
            {
                if (ParseInput(InputFiles[i]))
                {
                    Process();
                    FinalScore += WriteOutput();
                }
            }
            stopwatch.Stop();
            Console.WriteLine($"Overall Score: {FinalScore.ToString("n0")}");
            Console.WriteLine($"Overall Took {GetElapsedTime(stopwatch)} seconds");
            Console.ReadLine();
        }

        public static bool ParseInput(string filename)
        {
            if (!File.Exists($"{filename}.{FileExtension}"))
            {
                Console.WriteLine($"{Environment.NewLine}Dataset ({filename}.{FileExtension}) not found!");
                return false;
            }

            Filename = filename;
            Stopwatch.Restart();
            Console.WriteLine($"{Environment.NewLine}Dataset: {InputFile}");

            PhotosCount = 0;
            string[] photoData;
            using (StreamReader sr = File.OpenText(InputFile))
            {
                string s = sr.ReadLine();
                if (!string.IsNullOrEmpty(s))
                {
                    PhotosCount = int.Parse(s);
                }

                Photos = new Photo[PhotosCount];
                photoData = new string[PhotosCount];

                for (int id = 0; id < PhotosCount; id++)
                    photoData[id] = sr.ReadLine();
            }


            bool[] horizontal = new bool[PhotosCount];
            Parallel.For(0, PhotosCount, (int id) =>
            {
                string[] config = photoData[id].Split(' ');
                bool isHorizontal = config[0] == "H";
                int tagCount = int.Parse(config[1]);
#if HASHED_TAGS
                int[] tags = new int[tagCount];
#else
                string[] tags = new string[tagCount];
#endif
                for (int i = 0; i < tagCount; i++)
#if HASHED_TAGS
                    tags[i] = config[2 + i].GetHashCode();
#else
                    tags[i] = config[2 + i];
#endif
#if SORT_TAGS
                Array.Sort(tags);
#endif
                var photo = new Photo(id, isHorizontal, tags);
                Photos[id] = photo;
                horizontal[id] = isHorizontal;
            });
            HorizontalPhotosCount = horizontal.Where(b => b).Count();

            SlidesCount = (VerticalPhotosCount / 2) + HorizontalPhotosCount;
            Console.WriteLine($"PhotosCount: {PhotosCount.ToString("n0")}");
            Console.WriteLine($"SlidesCount: {SlidesCount.ToString("n0")}");
            Console.WriteLine($"Parsing Time: {GetElapsedTime(Stopwatch)}");
            return true;
        }
        public static void Process()
        {
            Stopwatch.Restart();
            Slides = new Slide[SlidesCount];

            int slideIndex = 0;
            for (int i = 0; i < PhotosCount - 1; i++)
            {
                if (Photos[i].IsUsed)
                    continue;
                Photo current = Photos[i];
                Slide slide1, slide2;

                current.IsUsed = true;
                if (Photos[i].IsHorizontal)
                {
                    slide1 = new Slide(current);
                }
                else
                {
                    Photo nextVertical = GetNextVerticalPhoto(i + 1);
                    nextVertical.IsUsed = true;
                    slide1 = new Slide(current, nextVertical);
                }
                Slides[slideIndex] = slide1;
                slideIndex++;
                Photo next = GetNextPhoto(i + 1, slide1.Tags);
                if (next == null)
                    continue;
                next.IsUsed = true;
                if (next.IsHorizontal)
                {
                    slide2 = new Slide(next);
                }
                else
                {
                    Photo nextVertical = GetNextVerticalPhoto(i + 1);

                    nextVertical.IsUsed = true;
                    slide2 = new Slide(next, nextVertical);
                }
                Slides[slideIndex] = slide2;
                slideIndex++;
            }


            Console.WriteLine($"Processing Time: {GetElapsedTime(Stopwatch)}");
        }
        public static int WriteOutput()
        {
            Stopwatch.Restart();
            File.Delete(OutputFile);
            int totalScore = 0;
            using (FileStream fs = File.OpenWrite(OutputFile))
            {
                byte[] byteArray = new UTF8Encoding(true).GetBytes($"{SlidesCount}");
                fs.Write(byteArray, 0, byteArray.Length);
                for (int slideIndex = 0; slideIndex < SlidesCount; slideIndex++)
                {
                    if (slideIndex < SlidesCount - 1)
                    {
                        if (Slides[slideIndex + 1] != null)
                            totalScore += GetTransitionScore(Slides[slideIndex], Slides[slideIndex + 1]);
                    }
                    if (Slides[slideIndex] == null)
                        break;
                    byteArray = new UTF8Encoding(true).GetBytes($"{Environment.NewLine}{Slides[slideIndex].ID}");
                    fs.Write(byteArray, 0, byteArray.Length);
                }
            }
            Console.WriteLine($"Output Time: {GetElapsedTime(Stopwatch)}");
            Console.WriteLine($"Set Score: {totalScore.ToString("n0")}");
            return totalScore;
        }
        private static string GetElapsedTime(Stopwatch stopwatch)
        {
            if (stopwatch.ElapsedMilliseconds < 1000)
                return $"{stopwatch.ElapsedMilliseconds} ms";
            else if (stopwatch.Elapsed.TotalSeconds < 60)
                return $"{stopwatch.Elapsed.TotalSeconds.ToString("f2")} seconds";
            else if (stopwatch.Elapsed.Seconds > 0)
                return $"{stopwatch.Elapsed.TotalMinutes.ToString("n0")} minutes and {stopwatch.Elapsed.Seconds.ToString("n0")} seconds";
            else
                return $"{stopwatch.Elapsed.TotalMinutes.ToString("n0")} minutes";
        }
        private static Photo GetNextVerticalPhoto(int startAt)
        {
            for (int i = startAt; i < PhotosCount; i++)
            {
                if (!Photos[i].IsUsed && !Photos[i].IsHorizontal)
                    return Photos[i];
            }
            return null;
        }
#if HASHED_TAGS
        private static Photo GetNextPhoto(int startAt, int[] intersect = null)
#else
        private static Photo GetNextPhoto(int startAt, string[] intersect = null)
#endif
        {
            if (intersect != null)
            {
                Photo photo = null;
                int intersectionLength = intersect.Length;
                int minIntersect = intersectionLength / 4;
                int minSide = minIntersect / 2;
                int maxIntersect = intersectionLength - minIntersect;
                while (photo == null)
                {
                    Parallel.For(startAt, PhotosCount, (i, loopState) =>
                    {
                        if (photo != null)
                            loopState.Stop();
                        if (Photos[i].IsUsed)
                            return;

#if HASHED_TAGS && SORT_TAGS
                        int intersectionCount = SortedIntArrayIntersectionCount(Photos[i].Tags, intersect);
#else
                        int intersectionCount = Photos[i].Tags.Intersect(intersect).Count();
#endif

                        int leftNotRight = intersectionLength - intersectionCount;
                        int rightNotLeft = Photos[i].Tags.Length - intersectionCount;
                        if (intersectionCount < minIntersect || intersectionCount > maxIntersect || leftNotRight < minSide || rightNotLeft < minSide)
                            return;

                        lock (ThreadLock)
                        {
                            photo = Photos[i];
                        }
                    });
                    if (photo == null)
                    {
                        if (minIntersect == 0 && maxIntersect == intersectionLength && minSide == 0)
                        {
                            for (int i = startAt; i < PhotosCount; i++)
                            {
                                if (Photos[i].IsUsed)
                                    continue;

                                return Photos[i];
                            }
                            if (photo == null)
                                return null;
                        }
                        else
                        {
                            if (minIntersect > 0)
                                minIntersect--;
                            if (maxIntersect < intersectionLength)
                                maxIntersect++;
                            if (minSide > 0)
                                minSide--;
                        }
                    }
                }
                return photo;
            }
            else
                for (int i = startAt; i < PhotosCount; i++)
                {
                    if (Photos[i].IsUsed)
                        continue;

                    return Photos[i];
                }
            return null;
        }
        private static int GetTransitionScore(Slide slide1, Slide slide2)
        {
            int common = slide1.Tags.Intersect(slide2.Tags).Count();
            int left = slide1.Tags.Length - common;
            int right = slide2.Tags.Length - common;

            if (left < common)
                common = left;

            if (right < common)
                common = right;
            return common;
        }

        private static int SortedIntArrayIntersectionCount(int[] array1, int[] array2)
        {
            int firstCount = array1.Length;
            int secondCount = array2.Length;
            int firstIndex = 0, secondIndex = 0;
            int intersectionCount = 0;

            while (firstIndex < firstCount && secondIndex < secondCount)
            {
                var comp = array1[firstIndex].CompareTo(array2[secondIndex]);
                if (comp < 0)
                    ++firstIndex;
                else if (comp > 0)
                    ++secondIndex;
                else
                {
                    intersectionCount++;
                    ++firstIndex;
                    ++secondIndex;
                }
            }
            return intersectionCount;
        }

        public class Photo
        {
            public bool IsUsed { get; set; }
            public bool IsHorizontal { get; private set; }
            public int ID { get; private set; }
#if HASHED_TAGS
            public int[] Tags { get; private set; }
            public Photo(int id, bool isHorizontal, int[] tags)
            {
                ID = id;
                IsHorizontal = isHorizontal;
                Tags = tags;
            }
#else
            public string[] Tags { get; private set; }
            public Photo(int id, bool isHorizontal, string[] tags)
            {
                ID = id;
                IsHorizontal = isHorizontal;
                Tags = tags;
            }
#endif
        }

        public class Slide
        {
            public Photo Photo1 { get; private set; }
            public Photo Photo2 { get; private set; }

            public string ID { get; private set; }

#if HASHED_TAGS
            public int[] Tags { get; private set; }
#else
            public string[] Tags { get; private set; }
#endif
            public Slide(Photo photo)
            {
                Photo1 = photo ?? throw new NullReferenceException();
                ID = photo.ID.ToString();
#if HASHED_TAGS
                Tags = new int[Photo1.Tags.Length];
                for (int i = 0; i < Photo1.Tags.Length; i++)
                    Tags[i] = Photo1.Tags[i];
#else
                Tags = new string[Photo1.Tags.Length];
                for (int i = 0; i < Photo1.Tags.Length; i++)
                    Tags[i] = Photo1.Tags[i];
#endif
            }

            public Slide(Photo photo1, Photo photo2)
            {
                Photo1 = photo1 ?? throw new NullReferenceException(); ;
                Photo2 = photo2 ?? throw new NullReferenceException(); ;

                if (Photo1.IsHorizontal || Photo2.IsHorizontal)
                    throw new InvalidOperationException("Only vertical photos can be combined.");
                ID = $"{photo1.ID} {photo2.ID}";
#if HASHED_TAGS
#if DISTINCT_TAGS
                var tagList = new System.Collections.Generic.List<int>(Math.Max(Photo1.Tags.Length, Photo2.Tags.Length));
                tagList.AddRange(Photo1.Tags);
                tagList.AddRange(Photo2.Tags);
                Tags = tagList.Distinct().ToArray();
#else
                Tags = new int[Photo1.Tags.Length + Photo2.Tags.Length];
                for (int i = 0; i < Photo1.Tags.Length; i++)
                    Tags[i] = Photo1.Tags[i];
                for (int i = 0; i < Photo2.Tags.Length; i++)
                    Tags[Photo1.Tags.Length + i] = Photo2.Tags[i];
#endif
#else
                Tags = new string[Photo1.Tags.Length + Photo2.Tags.Length];
                for (int i = 0; i < Photo1.Tags.Length; i++)
                    Tags[i] = Photo1.Tags[i];
                for (int i = 0; i < Photo2.Tags.Length; i++)
                    Tags[Photo1.Tags.Length + i] = Photo2.Tags[i];
#endif
            }
        }
    }
}
