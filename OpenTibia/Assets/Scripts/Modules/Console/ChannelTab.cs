﻿using OpenTibiaUnity.Core;
using OpenTibiaUnity.Core.Chat;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OpenTibiaUnity.Modules.Console
{
    internal enum ChannelButtonState
    {
        Active,
        Inactive,
        Flashing,

        // internal only
        FlashingFinished,
    }
    
    internal class ChannelTab : Core.Components.DraggableTabButton
    {
        private static Color s_ActiveColor = Colors.ColorFromRGB(MessageColors.White);
        private static Color s_InactiveColor = Colors.ColorFromRGB(MessageColors.Grey);
        private static Color s_FlashingColor = Colors.ColorFromRGB(MessageColors.Red);
        
        private const float FlashingDuration = 2f;
        private const float FlashingReverseAfter = 1f;

        internal class ChannelButtonClickedEvent : UnityEvent<ChannelTab> { }
        
        [SerializeField] private TMPro.TextMeshProUGUI m_ChannelNameLabel = null;

        internal ChannelButtonClickedEvent onClick = new ChannelButtonClickedEvent();
        internal Channel Channel;
        private ChannelButtonState m_State = ChannelButtonState.Active;
        private float m_FlashingTicks = 0;
        private float m_LastFlashingTime = 0;
        private bool m_LastColorIsFlashing = false;

        protected override void Start() {
            base.Start();

            GetComponent<Button>().onClick.AddListener(OnButtonClick);
        }

        protected void Update() {
            if (m_State == ChannelButtonState.Flashing) {
                m_FlashingTicks += Time.deltaTime;
                if (m_FlashingTicks >= FlashingDuration) {
                    m_State = ChannelButtonState.FlashingFinished;
                    m_ChannelNameLabel.color = s_FlashingColor;
                    return;
                }
                
                if (Time.time - m_LastFlashingTime >= FlashingReverseAfter) {
                    if (m_LastColorIsFlashing)
                        m_ChannelNameLabel.color = s_ActiveColor;
                    else
                        m_ChannelNameLabel.color = s_FlashingColor;

                    m_LastColorIsFlashing = !m_LastColorIsFlashing;
                    m_LastFlashingTime = Time.time;
                }
            }
        }

        internal void SetText(string text) {
            m_ChannelNameLabel.SetText(text);
            GetComponent<LayoutElement>().preferredWidth = Mathf.Max(120, m_ChannelNameLabel.preferredWidth * 1.5f);
        }

        internal void SetImage(Sprite sprite) {
            GetComponent<Image>().sprite = sprite;
        }

        internal void SetState(ChannelButtonState state) {
            if (state == ChannelButtonState.FlashingFinished)
                state = ChannelButtonState.Flashing;

            m_State = state;
            m_FlashingTicks = 0;
            m_LastFlashingTime = Time.time;
            m_LastColorIsFlashing = true;

            switch (state) {
                case ChannelButtonState.Active:
                    m_ChannelNameLabel.color = s_ActiveColor;
                    break;
                case ChannelButtonState.Inactive:
                    m_ChannelNameLabel.color = s_InactiveColor;
                    break;
                case ChannelButtonState.Flashing:
                    m_ChannelNameLabel.color = s_FlashingColor;
                    break;
            }
        }

        private void OnButtonClick() {
            onClick.Invoke(this);
        }
    }
}
