using System;
using System.Collections.Generic;
using System.Text;
using Emgu.Util;
using Emgu.CV.Structure;

namespace Emgu.CV
{
   /// <summary>
   /// The motion history class
   /// </summary>
   /// <remarks>
   /// For help on using this class, take a look at the Motion Detection example
   /// </remarks>
   public class MotionHistory : DisposableObject
   {
      private int _bufferMax;
      private int _diffThresh;
      private double _mhiDuration;
      private Image<Gray, Byte> _silh;
      private Image<Gray, Single> _mhi;
      private Image<Gray, Byte> _mask;
      private Image<Gray, Single> _orientation;
      private Image<Gray, Single> _segMask;

      private DateTime _initTime;
      private DateTime _lastTime;
      private double _maxTimeDelta;
      private double _minTimeDelta;

      private Queue<Image<Gray, Byte>> _buffer;

      /// <summary>
      /// The Motion Segment Mask. 
      /// Same as the seg_mask in cvSegmentMotion function
      /// Do not dispose this image.
      /// </summary>
      public Image<Gray, Single> SegmentMask
      {
         get
         {
            return _segMask;
         }
      }

      /// <summary>
      /// The motion mask. 
      /// Do not dispose this image.
      /// </summary>
      public Image<Gray, Byte> Mask
      {
         get
         {
            return _mask;
         }
      }

      /// <summary>
      /// Create a motion history object
      /// </summary>
      /// <param name="bufferCount">number of images to store in buffer, adjust it to fit your camera's frame rate</param>
      /// <param name="diffThresh">0-255, the amount of pixel intensity change to consider it as motion pixel</param>
      /// <param name="mhiDuration">In second, the duration of motion history you wants to keep</param>
      /// <param name="maxTimeDelta">In second. Any change happens between a time interval greater than this will not be considerred</param>
      /// <param name="minTimeDelta">In second. Any change happens between a time interval smaller than this will not be considerred.</param>
      public MotionHistory(int bufferCount, int diffThresh, double mhiDuration, double maxTimeDelta, double minTimeDelta)
         : this (bufferCount, diffThresh, mhiDuration, maxTimeDelta, minTimeDelta, DateTime.Now)
      {
      }

      /// <summary>
      /// Create a motion history object
      /// </summary>
      /// <param name="bufferCount">number of images to store in buffer, adjust it to fit your camera's frame rate</param>
      /// <param name="diffThresh">0-255, the amount of pixel intensity change to consider it as motion pixel</param>
      /// <param name="mhiDuration">In second, the duration of motion history you wants to keep</param>
      /// <param name="maxTimeDelta">In second. Any change happens between a time interval larger than this will not be considerred</param>
      /// <param name="minTimeDelta">In second. Any change happens between a time interval smaller than this will not be considerred.</param>
      /// <param name="startTime">The start time of the motion history</param>
      public MotionHistory(int bufferCount, int diffThresh, double mhiDuration, double maxTimeDelta, double minTimeDelta, DateTime startTime)
      {
         _bufferMax = bufferCount;
         _buffer = new Queue<Image<Gray, Byte>>(_bufferMax);
         _diffThresh = diffThresh;
         _mhiDuration = mhiDuration;
         _initTime = startTime;
         _maxTimeDelta = maxTimeDelta;
         _minTimeDelta = minTimeDelta;
      }

      /// <summary>
      /// Update the motion history with the specific image and current timestamp
      /// </summary>
      /// <param name="image">The image to be added to history</param>
      public void Update(Image<Gray, Byte> image)
      {
         Update(image, DateTime.Now);
      }

      /// <summary>
      /// Update the motion history with the specific image and the specific timestamp
      /// </summary>
      /// <param name="image">The image to be added to history</param>
      /// <param name="timestamp">The time when the image is captured</param>
      public void Update(Image<Gray, Byte> image, DateTime timestamp)
      {
         _lastTime = timestamp;
         TimeSpan ts = _lastTime.Subtract(_initTime);

         if (_buffer.Count == _bufferMax)
         {
            _buffer.Dequeue();
         }
         _buffer.Enqueue(image);

         if (_silh == null) _silh = image.CopyBlank();
         if (_mhi == null) _mhi = new Image<Gray, float>(image.Size);
         if (_mask == null) _mask = image.CopyBlank();
         if (_orientation == null) _orientation = new Image<Gray, float>(image.Size);

         CvInvoke.cvAbsDiff(image.Ptr, _buffer.Peek().Ptr, _silh.Ptr);
         CvInvoke.cvThreshold(_silh.Ptr, _silh.Ptr, _diffThresh, 1, Emgu.CV.CvEnum.THRESH.CV_THRESH_BINARY);

         CvInvoke.cvUpdateMotionHistory(_silh.Ptr, _mhi, ts.TotalSeconds, _mhiDuration);
         double scale = 255.0 / _mhiDuration;
         CvInvoke.cvConvertScale(_mhi.Ptr, _mask.Ptr, scale, (_mhiDuration - ts.TotalSeconds) * scale);

         CvInvoke.cvCalcMotionGradient(_mhi.Ptr, _mask.Ptr, _orientation.Ptr, _maxTimeDelta, _minTimeDelta, 3);
      }

      /// <summary>
      /// Get a sequence of motion component
      /// </summary>
      /// <param name="storage">The storage used by the motion components</param>
      /// <returns>A sequence of motion components</returns>
      public Seq<MCvConnectedComp> GetMotionComponents(MemStorage storage)
      {
         TimeSpan ts = _lastTime.Subtract(_initTime);
         if (_segMask == null)
            _segMask = new Image<Gray, float>(_mhi.Size);
         Seq<MCvConnectedComp> seq = new Seq<MCvConnectedComp>(CvInvoke.cvSegmentMotion(_mhi, _segMask, storage, ts.TotalSeconds, _maxTimeDelta), storage);
         return seq;
      }

      /// <summary>
      /// Given a rectagle area of the motion, output the angle of the motion and the number of pixels that are considered to be motion pixel 
      /// </summary>
      /// <param name="motionRectangle">The rectangle area of the motion</param>
      /// <param name="angle">The orientation of the motion</param>
      /// <param name="motionPixelCount">Number of motion pixels within silhoute ROI</param>
      public void MotionInfo(System.Drawing.Rectangle motionRectangle, out double angle, out double motionPixelCount)
      {
         TimeSpan ts = _lastTime.Subtract(_initTime);
         // select component ROI
         CvInvoke.cvSetImageROI(_silh, motionRectangle);
         CvInvoke.cvSetImageROI(_mhi, motionRectangle);
         CvInvoke.cvSetImageROI(_orientation, motionRectangle);
         CvInvoke.cvSetImageROI(_mask, motionRectangle);

         // calculate orientation
         angle = CvInvoke.cvCalcGlobalOrientation(_orientation.Ptr, _mask.Ptr, _mhi.Ptr, ts.TotalSeconds, _mhiDuration);
         angle = 360.0 - angle; // adjust for images with top-left origin

         // caculate number of points within silhoute ROI
         motionPixelCount = CvInvoke.cvNorm(_silh.Ptr, IntPtr.Zero, CvEnum.NORM_TYPE.CV_L1, IntPtr.Zero); // calculate number of points within silhouette ROI

         // reset the ROI
         CvInvoke.cvResetImageROI(_mhi);
         CvInvoke.cvResetImageROI(_orientation);
         CvInvoke.cvResetImageROI(_mask);
         CvInvoke.cvResetImageROI(_silh);
      }

      /// <summary>
      /// Release unmanaged resources
      /// </summary>
      protected override void DisposeObject()
      {

      }

      /// <summary>
      /// Release any images associated with this object
      /// </summary>
      protected override void ReleaseManagedResources()
      {
         if (_silh != null) _silh.Dispose();
         if (_mhi != null) _mhi.Dispose();
         if (_mask != null) _mask.Dispose();
         if (_orientation != null) _orientation.Dispose();
         if (_segMask != null) _segMask.Dispose();
      }
   }
}
