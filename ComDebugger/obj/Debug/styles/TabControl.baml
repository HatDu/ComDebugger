   M S B A M L   `   `   `  ����F  BComDebugger, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null)clr-namespace:ComDebuggerComDebugger  5      W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089   BorderBrush  �� Color  
HoverBrush  ForegroundBrush  TabControlBackgroundBrush �� EndPoint �� 
StartPoint � Color � Offset  TabItemPathBrush  TabControlStyle �� 
TargetType �� Property �� Value �� 
TargetType	 � RowDefinitions
 K� ZIndex �� CornerRadius �� Orientation K� IsItemsHost �Name F�Name �� ElementName*    $ComDebugger.ContentToMarginConverter    Value �� 	Converter(   "ComDebugger.ContentToPathConverter   Value � LayoutTransform �� ScaleY ��Name �� Path ��Name �� Triggers P� 
SourceName P� Property P� Value �� 
TargetName F� 	Direction F� Opacity  F� ShadowDepth! F� Softness�� 5      D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    :x,http://schemas.microsoft.com/winfx/2006/xaml     5      #localclr-namespace:ComDebugger  5      %!	  &	        &	 #     &	 F     &	 i     &	 j    &	     7   7  7   '��     7  7   7   7  7  �� 5   
   $  	#FFFFFFFF��6.   �� 5	   
   $  	#FFFF4500��6-   �� 5
   
   $  	#FF000000��62   �� 5   
   $ 0.5,0&�6@   $ 0.5,1&�6Q   .��5      � $ 	#FFa9cde7��6   $ 0Q�6-   � 5      $ 	#FFe7f4fc��6   $	 0.3Q�6-   � 5      $ 	#FFf2fafd��6   $
 0.85Q�6-   � 5      $ 	#FFe4f6fa��6   $ 1Q�6-   5      �� 5   
   $	 0,0&�67   $	 0,1&�6H   .��5      � $ 	#FF3164a5��6   $ 0Q�6-   � 5      $ 	#FFe4f6fa��6   $ 1Q�6-   5      �� 5   
   " ��6(   .�5      ��  � ��$ 1q�60   �� 5       � �� 5      �� 5      " ��6&   .R�5      � 	 5       �� 5!   "   $
��Auto �60   �� 5"   "   $��* �60   .+�5$      �� $��1��6%   $��2,0,2,2q�62   $
 2��6L   $ 0,0,2,2��6]   8��  8�� ._�5'   "   �� $��SelectedContent��63   5(      �� 5*      $ 
Horizontal=�6)   $��0��6B   $
 1��6O    . 6`   �� 5+      $��0��6(   $��2��65   $��Bottom=�6@   8�� 6[   5,      5-      5/      50      �� 54   
   " ��6)   .�55      ��  �@J�SnapsToDevicePixels . 64   �� 56       � �� 57      �� 58      " ��6&   .R�5:      � $	 grd��6   .+�5;      F� $ TabPath��6#   $��2��64   8��  8�� ��5<   #   �� $ TabItemContent��# Z ��5=   #   �� $ TabItemContent��# Z  5G   "   �� 5H   &   $ -1Q�65   5J      �� 5K      $ TabItemTopBorder��6(   $��2��6B   $y�Visible=�6M   $��Bottom=�5L   (   8�� ��5M   (   �� $ TabPath��$ Margin��� 5O      $ TabItemContent��6/   $��Header��6G   $��	10,2,10,2q�5P   /   $��Center=�6B   8�� 5Q   /   5R       5T      P� 5U      $	 grd��6J    �@J�IsMouseOver . . �5V   "   �� 8  $ TabPath��6_    � ��5W      P� 5X       �@��
IsSelected . . �5Y   "   �� $ TabPath��69    � �� 5Z   &   �� 5[   *   $  	#FFe4f6fa��6:   5]   #   �� 5^   "    �@J�BitmapEffect 5_   &   F� 5`   *   $	 302Q�6A   $	 0.4Q�6Q   $  2Q�6_   $	! 0.5Q�6o   5b   #   �� 5c   "    �@K�ZIndex$ 2��6A   �� 5d   "   $ TabItemTopBorder��6N    � y�$ Hidden=�6?   6N   5e      5g      5i      5j      5k      