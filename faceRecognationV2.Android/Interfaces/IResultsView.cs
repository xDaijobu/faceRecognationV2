using System;
using System.Collections.Generic;
using static faceRecognationV2.Interfaces.ISimilarityClassifier;

namespace faceRecognationV2.Interfaces
{
    public interface IResultsView
    {
        public void SetResults(List<Recognition> results);
    }
}
