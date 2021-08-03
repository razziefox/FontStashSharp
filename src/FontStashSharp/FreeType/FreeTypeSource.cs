﻿using FontStashSharp.Interfaces;
using SharpFont;
using SharpFont.Internal;
using System;
using System.Runtime.InteropServices;

namespace FontStashSharp
{
	internal class FreeTypeSource: IFontSource
	{
		private const float Dpi = 96;
		private const float PointsPerInch = 72;

		private static IntPtr _libraryHandle;
		private GCHandle _memoryHandle;
		private IntPtr _faceHandle;
		private readonly FaceRec _rec;
		private float AscentBase, DescentBase, LineHeightBase;

		public FreeTypeSource(byte[] data)
		{
			Error err;
			if (_libraryHandle == IntPtr.Zero)
			{
				IntPtr libraryRef;
				err = FTNative.FT_Init_FreeType(out libraryRef);

				if (err != Error.Ok)
					throw new FreeTypeException(err);

				_libraryHandle = libraryRef;
			}

			_memoryHandle = GCHandle.Alloc(data, GCHandleType.Pinned);

			IntPtr faceRef;
			err = FTNative.FT_New_Memory_Face(_libraryHandle, _memoryHandle.AddrOfPinnedObject(), data.Length, 0, out faceRef);

			if (err != Error.Ok)
				throw new FreeTypeException(err);

			_faceHandle = faceRef;
			_rec = PInvokeHelper.PtrToStructure<FaceRec>(_faceHandle);

			var fh = _rec.ascender - _rec.descender;
			AscentBase = _rec.ascender / (float)fh;
			DescentBase = _rec.descender / (float)fh;
			LineHeightBase = _rec.height / (float)fh;
		}

		~FreeTypeSource()
		{
			Dispose(false);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (_faceHandle != IntPtr.Zero)
			{
				FTNative.FT_Done_Face(_faceHandle);
				if (_memoryHandle.IsAllocated)
					_memoryHandle.Free();

				_faceHandle = IntPtr.Zero;
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		public int? GetGlyphId(int codepoint) => (int?)FTNative.FT_Get_Char_Index(_faceHandle, (uint)codepoint);

		public int GetGlyphKernAdvance(int previousGlyphId, int glyphId, int fontSize)
		{
			return 0;
		}

		private void SetPixelSizes(int width, int height)
		{
			var err = FTNative.FT_Set_Pixel_Sizes(_faceHandle, (uint)width, (uint)height);
			if (err != Error.Ok)
				throw new FreeTypeException(err);
		}

		private void LoadGlyph(int glyphId)
		{
			var err = FTNative.FT_Load_Glyph(_faceHandle, (uint)glyphId, (int)LoadFlags.Default | (int)LoadTarget.Normal);
			if (err != Error.Ok)
				throw new FreeTypeException(err);
		}

		private void GetCurrentGlyph(out GlyphSlotRec glyph)
		{
			glyph = PInvokeHelper.PtrToStructure<GlyphSlotRec>(_rec.glyph);
		}

		public void GetGlyphMetrics(int glyphId, int fontSize, out int advance, out int x0, out int y0, out int x1, out int y1)
		{
			SetPixelSizes(0, fontSize);
			LoadGlyph(glyphId);

			GlyphSlotRec glyph;
			GetCurrentGlyph(out glyph);
			advance = (int)glyph.advanceX >> 6;
			x0 = (int)glyph.metrics.horiBearingX >> 6;
			y0 = -(int)glyph.metrics.horiBearingY >> 6;
			x1 = x0 + ((int)glyph.metrics.width >> 6);
			y1 = y0 + ((int)glyph.metrics.height >> 6);
		}

		public void GetMetricsForSize(int fontSize, out float ascent, out float descent, out float lineHeight)
		{
			ascent = fontSize * AscentBase;
			descent = fontSize * DescentBase;
			lineHeight = fontSize * LineHeightBase;
		}

		public unsafe void RasterizeGlyphBitmap(int glyphId, int fontSize, byte[] buffer, int startIndex, int outWidth, int outHeight, int outStride)
		{
			SetPixelSizes(0, fontSize);
			LoadGlyph(glyphId);

			FTNative.FT_Render_Glyph(_rec.glyph, (int)RenderMode.Normal);

			GlyphSlotRec glyph;
			GetCurrentGlyph(out glyph);
			var ftbmp = glyph.bitmap;

			fixed (byte* bptr = buffer)
			{
				for (var y = 0; y < outHeight; ++y)
				{
					var pos = (y * outStride) + startIndex;

					byte* dst = bptr + pos;
					byte* src = (byte*)ftbmp.buffer + y * ftbmp.pitch;
					for (var x = 0; x < outWidth; ++x)
					{
						*dst++ = *src++;
					}
				}
			}
		}
	}
}
