using UnityEngine;
using System.Collections.Generic;

public class HMD_TMO_Demo : MonoBehaviour {

    // Panorama
    [System.Serializable]
    public struct Panoramas {
        public Cubemap m_hdrCubemap;
        public Texture2D m_hdrPanorama;
    };
    public List<Panoramas> m_panoramas;
    int m_currentPanorama = 0;

    RenderTexture m_rtPanorama;

    // Compute shader
    public ComputeShader m_histogramComputeShader;
    public ComputeShader m_keyValuesComputeShader;
    int m_keyValuesKernel;
    int m_histogramKernel;

    // Tone Mapping shader
    public Shader m_HmdTmoShader;
    Material m_HmdTmoMaterial;

    // Skybox material
    public Material m_panoramaMaterial;

    // Viewport TMO
    ComputeBuffer m_keyValuesBuffer;
    uint[] m_keyValuesArray;
    uint[] m_clearKeyValuesArray;
    Vector4 m_keyValuesVector;

    // GLobal TMO
    int m_nbBins;
    ComputeBuffer m_histogramBuffer;
    uint[] m_histogramArray;
    uint[] m_clearHistogramArray;

    // Variables
    public float m_LwMin;
    public float m_LwMax;
    public float m_LwAvg;
    float m_normLogMin = 5.0f;
    float m_normLogMax = 15.0f;

    [Range(0.0f, 1.0f)]
    public float m_exposureValue = 0.18f;

    [Range(0.0f, 1.0f)]
    public float m_saturation = 0.8f;

    bool m_eyeAdaptation;
    public float m_adaptedSpeed = 1.0f;

    // Demonstration output
    public enum Output {
        HDR,
        Linear,
        Our,
        Yu,
        Global,
        Viewport,
        Log
    };
    public Output m_output;

    // Use this for initialization
    void Start() {
        // Init rendering material
        m_HmdTmoMaterial = new Material(m_HmdTmoShader);
        
        // Init Viewport key values
        m_keyValuesBuffer = new ComputeBuffer(4, sizeof(uint));
        m_keyValuesArray = new uint[4];
        m_clearKeyValuesArray = new uint[4];
        m_clearKeyValuesArray[0] = uint.MaxValue;
        m_keyValuesVector = Vector4.zero;

        // Set first Panorama
        SetPanorama(0);

        // Init demo scenario
        m_output = Output.HDR;
        m_exposureValue = 0.5f;
        m_eyeAdaptation = false;
    }

    void Update() {
        // Change panorama
        if (Input.GetKeyDown(KeyCode.Keypad0)) {
            m_currentPanorama++;
            if (m_currentPanorama >= m_panoramas.Count)
                m_currentPanorama = 0;

            SetPanorama(m_currentPanorama);
            m_output = Output.HDR;
            m_exposureValue = 0.5f;
        }

        // Change output
        if (Input.GetKeyDown(KeyCode.Keypad6)) {
            if(m_output != Output.Log)
                m_output++;
        }
        if (Input.GetKeyDown(KeyCode.Keypad4)) {
            if(m_output != Output.HDR)
                m_output--;
        }

        // Eye adaptation
        if (m_output == Output.Yu || m_output == Output.Our)
            m_eyeAdaptation = true;
        else
            m_eyeAdaptation = false;

        // Change exposure
        if (Input.GetKey(KeyCode.Keypad8)) {
            m_exposureValue += Time.deltaTime*0.5f;
            if (m_exposureValue > 1.0f) m_exposureValue = 1.0f;
        }
        if (Input.GetKey(KeyCode.Keypad2)) {
            m_exposureValue -= Time.deltaTime*0.5f;
            if (m_exposureValue < 0.0f) m_exposureValue = 0.0f;
        }
        if (Input.GetKeyDown(KeyCode.Keypad5)) {
            m_exposureValue = 0.018f;
        }
    }
    

    [ImageEffectTransformsToLDR]
    private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        m_keyValuesBuffer.SetData(m_clearKeyValuesArray);
        m_keyValuesComputeShader.SetTexture(m_keyValuesKernel, "_Source", source);
        m_keyValuesComputeShader.SetBuffer(m_keyValuesKernel, "_KeyValues", m_keyValuesBuffer);
        m_keyValuesComputeShader.Dispatch(m_keyValuesKernel, Mathf.CeilToInt(source.width / 8.0f), Mathf.CeilToInt(source.height / 8.0f), 1);
        m_keyValuesBuffer.GetData(m_keyValuesArray);

        // Compute current black value
        float blackLum = m_keyValuesArray[0] / Mathf.Pow(2.0f, 10.0f);
        blackLum = Mathf.Exp((blackLum * m_normLogMax) - m_normLogMin);

        // Compute current white value
        float whiteLum = m_keyValuesArray[1] / Mathf.Pow(2.0f, 10.0f);
        whiteLum = Mathf.Exp((whiteLum * m_normLogMax) - m_normLogMin);

        // Compute current key value
        float avgNormLogLum = m_keyValuesArray[2] / Mathf.Pow(2.0f, 10.0f);
        float avgLogLum = (avgNormLogLum * m_normLogMax) - ((float)(source.width * source.height) * m_normLogMin);
        float keyValue = Mathf.Exp(avgLogLum / (float)(source.width * source.height));

        // Eye adaptation
        float timeAdaptation = m_eyeAdaptation ? Time.deltaTime * m_adaptedSpeed : 1.0f;
        m_keyValuesVector[0] = Mathf.Lerp(m_keyValuesVector[0], blackLum, timeAdaptation);
        m_keyValuesVector[1] = Mathf.Lerp(m_keyValuesVector[1], whiteLum, timeAdaptation);
        m_keyValuesVector[2] = Mathf.Lerp(m_keyValuesVector[2], keyValue, timeAdaptation);
        m_keyValuesVector[3] = m_exposureValue;

        // Update rendering material
        m_HmdTmoMaterial.SetVector("_KeyValues", m_keyValuesVector);
        m_HmdTmoMaterial.SetFloat("_Saturation", m_saturation);
        m_HmdTmoMaterial.SetInt("_Output", (int)m_output);
        Graphics.Blit(source, destination, m_HmdTmoMaterial);
    }

    void SetPanorama(int panoramaIndex) {
        // Update skybox
        m_panoramaMaterial.SetTexture("_Tex", m_panoramas[panoramaIndex].m_hdrCubemap);

        // Copy HDR panorama texture to RenderTexture in order to process ComputeShaders
        Texture2D hdrPanorama = m_panoramas[panoramaIndex].m_hdrPanorama;
        m_rtPanorama = new RenderTexture(hdrPanorama.width, hdrPanorama.height, 0);
        m_rtPanorama.format = RenderTextureFormat.DefaultHDR;
        RenderTexture.active = m_rtPanorama;
        Graphics.Blit(hdrPanorama, m_rtPanorama);
        RenderTexture.active = null;

        // Compute Panorama Key Values
        ComputeBuffer panoramaKeyValuesBuffer = new ComputeBuffer(4, sizeof(uint));
        uint[] panoramaKeyValuesArray = new uint[4];
        panoramaKeyValuesArray[0] = uint.MaxValue;
        panoramaKeyValuesBuffer.SetData(panoramaKeyValuesArray);
        m_keyValuesKernel = m_keyValuesComputeShader.FindKernel("KeyValues");
        m_keyValuesComputeShader.SetTexture(m_keyValuesKernel, "_Source", m_rtPanorama);
        m_keyValuesComputeShader.SetBuffer(m_keyValuesKernel, "_KeyValues", panoramaKeyValuesBuffer);
        m_keyValuesComputeShader.Dispatch(m_keyValuesKernel, Mathf.CeilToInt(m_rtPanorama.width / 8.0f), Mathf.CeilToInt(m_rtPanorama.height / 8.0f), 1);
        panoramaKeyValuesBuffer.GetData(panoramaKeyValuesArray);
        panoramaKeyValuesBuffer.Release();

        // Compute min/max/key values
        float blackLum = panoramaKeyValuesArray[0] / Mathf.Pow(2.0f, 10.0f);
        m_LwMin = Mathf.Exp((blackLum * m_normLogMax) - m_normLogMin);

        float whiteLum = panoramaKeyValuesArray[1] / Mathf.Pow(2.0f, 10.0f);
        m_LwMax = Mathf.Exp((whiteLum * m_normLogMax) - m_normLogMin);

        float avgNormLogLum = panoramaKeyValuesArray[2] / Mathf.Pow(2.0f, 10.0f);
        float avgLogLum = (avgNormLogLum * m_normLogMax) - ((float)(m_rtPanorama.width * m_rtPanorama.height) * m_normLogMin);
        m_LwAvg = Mathf.Exp(avgLogLum / (float)(m_rtPanorama.width * m_rtPanorama.height));

        // Compute Panorama Histogram
        m_nbBins = 500;
        m_histogramBuffer = new ComputeBuffer(m_nbBins, sizeof(uint));
        m_histogramArray = new uint[m_nbBins];
        m_clearHistogramArray = new uint[m_nbBins];
        m_histogramBuffer.SetData(m_clearHistogramArray);
        m_histogramKernel = m_histogramComputeShader.FindKernel("Histogram");
        m_histogramComputeShader.SetFloat("_LwMin", m_LwMin);
        m_histogramComputeShader.SetFloat("_LwMax", m_LwMax);
        m_histogramComputeShader.SetInt("_HistogramBins", m_nbBins);
        m_histogramComputeShader.SetBuffer(m_histogramKernel, "_Histogram", m_histogramBuffer);
        m_histogramComputeShader.SetTexture(m_histogramKernel, "_Source", m_rtPanorama);
        m_histogramComputeShader.Dispatch(m_histogramKernel, Mathf.CeilToInt(m_rtPanorama.width / 8.0f), Mathf.CeilToInt(m_rtPanorama.height / 8.0f), 1);

        // Cumulative histogram
        m_histogramBuffer.GetData(m_histogramArray);
        for (int i = 1; i < m_nbBins; i++) {
            m_histogramArray[i] += m_histogramArray[i - 1];
        }
        m_histogramBuffer.SetData(m_histogramArray);
        
        // Init rendering material
        m_HmdTmoMaterial.SetFloat("_MinPanoramaLum", m_LwMin);
        m_HmdTmoMaterial.SetFloat("_MaxPanoramaLum", m_LwMax);
        m_HmdTmoMaterial.SetFloat("_AvgPanoramaLum", m_LwAvg);
        m_HmdTmoMaterial.SetInt("_NbBins", m_nbBins);
        m_HmdTmoMaterial.SetBuffer("_Histogram", m_histogramBuffer);
    }

    void OnDestroy() {
        m_keyValuesBuffer.Release();
        m_keyValuesBuffer = null;
        m_histogramBuffer.Release();
        m_histogramBuffer = null;
    }
}
