using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Android.Content;
using Android.Graphics;
using Android.Util;
using Android.Views;

namespace faceRecognationV2.Droid.CustomViews
{
    public class OverlayView : View
    {
        List<DrawCallback> callbacks = new List<DrawCallback>();
        public OverlayView(Context context) : base(context)
        {
        }

        public OverlayView(Context context, IAttributeSet attrs) : base(context, attrs)
        {
        }

        public OverlayView(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle)
        {
        }



        public void AddCallback(DrawCallback callback)
        {
            callbacks.Add(callback);
        }

        //alternatif nya (C#), jikalau caara ygdibawah ini ngga bisa
        //private object _locker = new object();
        //public override void Draw(Canvas canvas)
        //{
        //    lock (_locker)
        //    {
        //        foreach(var callback in callbacks)
        //            callback.DrawCallback(canvas);
        //    };
        //}

        [MethodImpl(MethodImplOptions.Synchronized)]
        public override void Draw(Canvas canvas)
        {
            foreach (var callback in callbacks)
                callback.DrawCallback(canvas);
        }

        public interface DrawCallback
        {
            public void DrawCallback(Canvas canvas);
        }
    }
}
