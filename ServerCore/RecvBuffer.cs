using System;
using System.Collections.Generic;
using System.Text;

namespace ServerCore
{
	public class RecvBuffer
	{
		// [r][][w][][][][][][][]
		ArraySegment<byte> _buffer;
		int _readPos;
		int _writePos;

		public RecvBuffer(int bufferSize)
		{
			_buffer = new ArraySegment<byte>(new byte[bufferSize], 0, bufferSize);
		}

		/// <summary>
		/// 데이터 범위 사이즈
		/// </summary>
		public int DataSize { get { return _writePos - _readPos; } }

		/// <summary>
		/// 남은 범위 사이즈
		/// </summary>
		public int FreeSize { get { return _buffer.Count - _writePos; } }

    /// <summary>
    /// 데이터 바이트
    /// </summary>
    public ArraySegment<byte> ReadSegment
		{
			get { return new ArraySegment<byte>(_buffer.Array, _buffer.Offset + _readPos, DataSize); }
		}
		/// <summary>
		/// 남은 바이트
		/// </summary>
		public ArraySegment<byte> WriteSegment
		{
			get { return new ArraySegment<byte>(_buffer.Array, _buffer.Offset + _writePos, FreeSize); }
		}
    /// <summary>
    /// 시작 : [][][][][r][][w][][][]
		/// 일경우 Array.copy를 사용해서 r를 시작위치 0 으로 돌리고 w도 같이 옮긴다
		/// 결과 : [r][][w][][][][][][][]
    /// </summary>
    public void Clean()
		{
			int dataSize = DataSize;
			if (dataSize == 0)
			{
				// 남은 데이터가 없으면 복사하지 않고 커서 위치만 리셋
				_readPos = _writePos = 0;
			}
			else
			{
				// 남은 찌끄레기가 있으면 시작 위치로 복사
				Array.Copy(_buffer.Array, _buffer.Offset + _readPos, _buffer.Array, _buffer.Offset, dataSize);
				_readPos = 0;
				_writePos = dataSize;
			}
		}

		public bool OnRead(int numOfBytes)
		{
			if (numOfBytes > DataSize)
				return false;

			_readPos += numOfBytes;
			return true;
		}

		public bool OnWrite(int numOfBytes)
		{
			if (numOfBytes > FreeSize)
				return false;

			_writePos += numOfBytes;
			return true;
		}
	}
}
