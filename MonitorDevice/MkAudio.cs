using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NAudio.Wave;

namespace MonitorDevice
{
    class MkAudio
    {
        public Action<double[]> DataAvailable = null;
        WaveIn m_WaveIn = null;
        List<int> m_WavSampleList = new List<int>();
        public void StartAudioIn()
        {
            m_WaveIn = new WaveIn();

            m_WaveIn.DataAvailable += new EventHandler<WaveInEventArgs>(WaveIn_DataAvailable);
            //m_WaveIn.WaveFormat = new WaveFormat(44100, 32, 2);
            m_WaveIn.WaveFormat = new WaveFormat(44100, 16, 1);
            m_WaveIn.StartRecording();
        }

        public void StopAudioIn()
        {
            m_WaveIn.StopRecording();
        }

        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            //m_BufferProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
            m_WavSampleList.Clear();
            for (int i = 0; i < e.Buffer.Length; i += 2)
            {
                m_WavSampleList.Add(BitConverter.ToInt16(e.Buffer, i));
            }
            double[] normalizeArray = Normalize(m_WavSampleList);
            if (DataAvailable != null)
            {
                DataAvailable(normalizeArray);
            }
        }

        private double[] Normalize(List<int> list)
        {
            double scaleMax = 1;
            double scaleMin = -1;
            double valueMax = Int16.MaxValue / 10;
            double valueMin = Int16.MinValue / 10;
            double valueRange = valueMax - valueMin;
            double scaleRange = scaleMax - scaleMin;
            IEnumerable<double> result = list.Select(i => ((scaleRange * (i - valueMin)) / valueRange) + scaleMin);
            return result.ToArray();
        }
    }
}
