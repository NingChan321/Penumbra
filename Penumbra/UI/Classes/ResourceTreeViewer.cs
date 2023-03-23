using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui;
using Penumbra.Interop;

namespace Penumbra.UI.Classes;

public class ResourceTreeViewer
{
    private readonly string                             _name;
    private readonly int                                _actionCapacity;
    private readonly Action                             _onRefresh;
    private readonly Action<ResourceTree.Node, Vector2> _drawActions;
    private readonly HashSet<ResourceTree.Node>         _unfolded;
    private          ResourceTree[]?                    _trees;

    public ResourceTreeViewer( string name, int actionCapacity, Action onRefresh, Action<ResourceTree.Node, Vector2> drawActions )
    {
        _name           = name;
        _actionCapacity = actionCapacity;
        _onRefresh      = onRefresh;
        _drawActions    = drawActions;
        _unfolded       = new();
        _trees          = null;
    }

    public void Draw()
    {
        if( ImGui.Button( "Refresh Character List" ) )
        {
            try
            {
                _trees = ResourceTree.FromObjectTable();
            }
            catch( Exception e )
            {
                Penumbra.Log.Error( $"Could not get character list for {_name}:\n{e}" );
                _trees = Array.Empty<ResourceTree>();
            }
            _unfolded.Clear();
            _onRefresh();
        }

        try
        {
            _trees ??= ResourceTree.FromObjectTable();
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Could not get character list for {_name}:\n{e}" );
            _trees ??= Array.Empty<ResourceTree>();
        }

        var textColorNonPlayer = ImGui.GetColorU32( ImGuiCol.Text );
        var textColorPlayer    = ( textColorNonPlayer & 0xFF000000u ) | ( ( textColorNonPlayer & 0x00FEFEFE ) >> 1 ) | 0x8000u; // Half green

        foreach( var (tree, index) in _trees.WithIndex() )
        {
            using( var c = ImRaii.PushColor( ImGuiCol.Text, tree.PlayerRelated ? textColorPlayer : textColorNonPlayer ) )
            {
                if( !ImGui.CollapsingHeader( $"{tree.Name}##{index}", ( index == 0 ) ? ImGuiTreeNodeFlags.DefaultOpen : 0 ) )
                {
                    continue;
                }
            }
            using var id = ImRaii.PushId( index );

            ImGui.Text( $"Collection: {tree.CollectionName}" );

            using var table = ImRaii.Table( "##ResourceTree", ( _actionCapacity > 0 ) ? 4 : 3,
                ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg );
            if( !table )
            {
                continue;
            }

            ImGui.TableSetupColumn( string.Empty , ImGuiTableColumnFlags.WidthStretch, 0.2f );
            ImGui.TableSetupColumn( "Game Path"  , ImGuiTableColumnFlags.WidthStretch, 0.3f );
            ImGui.TableSetupColumn( "Actual Path", ImGuiTableColumnFlags.WidthStretch, 0.5f );
            if( _actionCapacity > 0 )
            {
                ImGui.TableSetupColumn( string.Empty, ImGuiTableColumnFlags.WidthFixed, (_actionCapacity - 1) * 3 * ImGuiHelpers.GlobalScale + _actionCapacity * ImGui.GetFrameHeight() );
            }
            ImGui.TableHeadersRow();

            DrawNodes( tree.Nodes, 0 );
        }
    }

    private void DrawNodes( IEnumerable<ResourceTree.Node> resourceNodes, int level )
    {
        var debugMode = Penumbra.Config.DebugMode;
        var frameHeight = ImGui.GetFrameHeight();
        var cellHeight = ( _actionCapacity > 0 ) ? frameHeight : 0.0f;
        foreach( var (resourceNode, index) in resourceNodes.WithIndex() )
        {
            if( resourceNode.Internal && !debugMode )
            {
                continue;
            }
            using var id = ImRaii.PushId( index );
            ImGui.TableNextColumn();
            var unfolded = _unfolded!.Contains( resourceNode );
            using( var indent = ImRaii.PushIndent( level ) )
            {
                ImGui.TableHeader( ( ( resourceNode.Children.Count > 0 ) ? ( unfolded ? "[-] " : "[+] " ) : string.Empty ) + resourceNode.Name );
                if( ImGui.IsItemClicked() && resourceNode.Children.Count > 0 )
                {
                    if( unfolded )
                    {
                        _unfolded.Remove( resourceNode );
                    }
                    else
                    {
                        _unfolded.Add( resourceNode );
                    }
                    unfolded = !unfolded;
                }
                if( debugMode )
                {
                    ImGuiUtil.HoverTooltip( $"Resource Type: {resourceNode.Type}\nSource Address: 0x{resourceNode.SourceAddress.ToString( "X" + nint.Size * 2 )}" );
                }
            }
            ImGui.TableNextColumn();
            var hasGamePaths = resourceNode.PossibleGamePaths.Length > 0;
            ImGui.Selectable( resourceNode.PossibleGamePaths.Length switch
            {
                0 => "(none)",
                1 => resourceNode.GamePath.ToString(),
                _ => "(multiple)",
            }, false, hasGamePaths ? 0 : ImGuiSelectableFlags.Disabled, new Vector2( ImGui.GetContentRegionAvail().X, cellHeight ) );
            if( hasGamePaths )
            {
                var allPaths = string.Join( '\n', resourceNode.PossibleGamePaths );
                if( ImGui.IsItemClicked() )
                {
                    ImGui.SetClipboardText( allPaths );
                }
                ImGuiUtil.HoverTooltip( $"{allPaths}\n\nClick to copy to clipboard." );
            }
            ImGui.TableNextColumn();
            if( resourceNode.FullPath.FullName.Length > 0 )
            {
                ImGui.Selectable( resourceNode.FullPath.ToString(), false, 0, new Vector2( ImGui.GetContentRegionAvail().X, cellHeight ) );
                if( ImGui.IsItemClicked() )
                {
                    ImGui.SetClipboardText( resourceNode.FullPath.ToString() );
                }
                ImGuiUtil.HoverTooltip( $"{resourceNode.FullPath}\n\nClick to copy to clipboard." );
            }
            else
            {
                ImGui.Selectable( "(unavailable)", false, ImGuiSelectableFlags.Disabled, new Vector2( ImGui.GetContentRegionAvail().X, cellHeight ) );
                ImGuiUtil.HoverTooltip( "The actual path to this file is unavailable.\nIt may be managed by another plug-in." );
            }
            if( _actionCapacity > 0 )
            {
                ImGui.TableNextColumn();
                using( var spacing = ImRaii.PushStyle( ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing with { X = 3 * ImGuiHelpers.GlobalScale } ) )
                {
                    _drawActions( resourceNode, new Vector2( frameHeight ) );
                }
            }
            if( unfolded )
            {
                DrawNodes( resourceNode.Children, level + 1 );
            }
        }
    }
}
