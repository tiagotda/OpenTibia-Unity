﻿namespace OpenTibiaUnity.Core.Input.GameAction
{
    internal class TurnActionImpl : IActionImpl
    {

        UnityEngine.Vector3Int m_Absolute;
        Appearances.AppearanceType m_AppearanceType;
        int m_StackPos;

        internal TurnActionImpl(UnityEngine.Vector3Int absolute, Appearances.ObjectInstance @object, int stackPos) {
            m_Absolute = absolute;
            m_AppearanceType = @object?.Type ?? throw new System.ArgumentNullException("ToggleWrapStateActionImpl.ToggleWrapStateActionImpl: Invalid object.");
            m_StackPos = stackPos;
        }

        public void Perform(bool _ = false) {
            var protocolGame = OpenTibiaUnity.ProtocolGame;
            if (!!protocolGame && protocolGame.IsGameRunning)
                protocolGame.SendTurnObject(m_Absolute, m_AppearanceType.ID, m_StackPos);
        }
    }
}
