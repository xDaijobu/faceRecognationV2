using System;
using System.Collections.Generic;
using Android.Graphics;

namespace faceRecognationV2.Interfaces
{
    public interface ISimilarityClassifier
    {
        void Register(string name, Recognition recognition);

        List<Recognition> RecognizeImage(Bitmap bitmap, bool storeExtra);

        void EnableStatLogging(bool debug);

        string GetStatString();

        void Close();

        void SetNumThreads(int numberThreads);

        void SetUseNNAPI(bool isChecked);

        public class Recognition
        {
            public string Id { get; }
            public string Title { get; }
            public float? Distance { get; }
            public object Extra { get; set; }
            public RectF Location { get; set; }
            public int? Color { get; set; }
            public Bitmap Crop { get; set; }

            public Recognition(string id, string title, float distance, RectF location)
            {
                Id = id;
                Title = title;
                Distance = distance;
                Location = location;
                Color = null;
                Extra = null;
                Crop = null;
            }

            public override string ToString()
            {
                string resultString = "";

                if (Id != null)
                    resultString += "[" + Id + "] ";

                if (Title != null)
                    resultString += Title + " ";

                if (Distance != null) // (%.1f%%)
                    resultString += Math.Round((float)Distance, 1) * 100f;

                if (Location != null)
                    resultString += Location + " ";

                return resultString.Trim();
            }
        }
    }
}
