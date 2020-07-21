using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Timers;
using System.IO.Ports;
using System.Threading;

namespace Sim
{
	/// <summary>
	/// MainWindow.xaml 的交互逻辑
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow( )
		{
			InitializeComponent ( );
			for(int index = 0;index < PationOnoffStatus.Length; index++) {
				PationOnoffStatus[ index ] = true;
				PationRegisterStatus[ index ] = true;
			}
			for(int index = 0;index < AmpRegisterStatus.Length; index++) {
				AmpRegisterStatus[ index ] = true;
			}
		}

		//定时器，用于循环显示串口接收到的数据
		System.Timers.Timer timer;
		CancellationTokenSource cts = new CancellationTokenSource();
		SerialPort serialPort;
		string serial_name = string.Empty;
		int serial_baudrate = 0;
		int last_received_count = 0;

		bool[] PationOnoffStatus = new bool[ 128 ];
		bool[] PationHiddenStatus = new bool[ 128 ];
		bool[] PationErrorStatus = new bool[ 128 ];
		bool[] PationRegisterStatus = new bool[ 128 ];
		bool[] PationSpeakerErrorStatus = new bool[ 128 ];
		bool[] AmpRegisterStatus = new bool[ 21 ];
		bool[] AmpHiddenStatus = new bool[ 21 ];
		bool[,] AmpWorkingStatus = new bool[ 21, 4 ];

		ushort common_status = 0x0000; //用于通用查询的状态标记

		private void CheckBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (!((e.Key == Key.Enter) || (e.Key == Key.Up) || (e.Key == Key.Down) || (e.Key == Key.Tab))) {
				e.Handled = true;
				return;
			}

			if (e.Key == Key.Enter) {
				CheckBox chk = sender as CheckBox;
				if (( bool )chk.IsChecked == false) {
					chk.IsChecked = true;
				} else {
					chk.IsChecked = false;
				}
			}
		}

		private void RadioButton_KeyDown(object sender, KeyEventArgs e)
		{
			if (!((e.Key == Key.Enter) || (e.Key == Key.Up) || (e.Key == Key.Down) || (e.Key == Key.Tab))) {
				e.Handled = true;
				return;
			}

			if (e.Key == Key.Enter) {
				RadioButton rdb = sender as RadioButton;
				if (( bool )rdb.IsChecked == false) {
					rdb.IsChecked = true;
					//刷新对应的细节状态标志
					for (int index = 0; index < AmpHiddenStatus.Length; index++) {
						if(wrpAmpSetting_stuaus.Children[index] == sender) {
							for(int index_1 = 0; index_1 < 4; index_1++) {

								CheckBox chk = wrpAmpSetting_stuaus.Children[ index_1 + 21 ] as CheckBox;
								if (AmpWorkingStatus[ index, index_1 ]) {
									chk.IsChecked = true;
								} else {
									chk.IsChecked = false;
								}
							}
						}
					}
				} 
			}
		}

		private void CobSerialport_PreviewMouseDown(object sender, MouseButtonEventArgs e)
		{
			ComboBox cob = sender as ComboBox;
			/*获取当前电脑上的串口集合，并将其显示在cob控件中*/
			string[] SerialPortlist = SerialPort.GetPortNames();
			cob.Items.Clear();
			foreach (string name in SerialPortlist) {
				SerialPort serialPort = new SerialPort( name, 9600, Parity.Mark, 8, StopBits.One );
				try {
					serialPort.Open();
					serialPort.Close();
					cob.Items.Add( name );
				} catch {
					;
				}
			}
		}

		private void CobSerialport_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ComboBox cob = sender as ComboBox;
			if (cob.SelectedIndex < 0) {
				serial_name = string.Empty;
				return;
			}

			serial_name = cob.SelectedItem.ToString();
			if (timer != null) {
				timer.Enabled = false; //暂时不使用定时器循环检查
			}
			if (serialPort != null) {
				if (serialPort.IsOpen) {
					serialPort.Close();
				}
			}
		}

		private void CobBaudrate_PreviewMouseDown(object sender, MouseButtonEventArgs e)
		{
			ComboBox cob = sender as ComboBox;
			cob.Items.Clear();
			cob.Items.Add( "1200" );
			cob.Items.Add( "2400" );
			cob.Items.Add( "4800" );
			cob.Items.Add( "9600" );
			cob.Items.Add( "19200" );
			cob.Items.Add( "38400" );
			cob.Items.Add( "57600" );
			cob.Items.Add( "115200" );
		}

		private void CobBaudrate_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ComboBox cob = sender as ComboBox;
			if (cob.SelectedIndex < 0) {
				serial_baudrate = 0;
				return;     
			}

			serial_baudrate = Convert.ToInt32( cob.SelectedItem );
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			//串口初始化、定时器使能（检查分区及功放状态）
			if ((serial_name == string.Empty) || (serial_baudrate == 0)) {
				MessageBox.Show( "请选择正常的串口名及波特率" );
				return;
			} else {
				//实例化一个串口对象  用于仿真和通讯转接板之间的通讯
				serialPort = new SerialPort( serial_name, serial_baudrate, Parity.None, 8, StopBits.One );
				serialPort.Open();
			}

			if (timer == null) {
				//开启定时器，用于实时刷新进度条、测试环节、测试项、测试值
				timer = new System.Timers.Timer( 30 );   //实例化Timer类，设置间隔时间单位毫秒
				timer.Elapsed += new ElapsedEventHandler( UpdateWork ); //到达时间的时候执行事件；     
				timer.AutoReset = true;   //设置是执行一次（false）还是一直执行(true)；     
				timer.Enabled = true;     //是否执行System.Timers.Timer.Elapsed事件； 
			} else {
				timer.Enabled = true; //允许重新使能定时器检测
			}

			cobBaudrate.Visibility = Visibility.Hidden;
		}

		private delegate object dlg_CheckedstatusGet(int index, WrapPanel wrapPanel, bool is_checkbox);

		object CheckedstatusGet(int index ,WrapPanel wrapPanel ,bool is_checkbox)
		{
			if (is_checkbox) {
				CheckBox chk = wrapPanel.Children[ index ] as CheckBox;
				return chk.IsChecked;
			} else {
				RadioButton rdb = wrpAmpSetting_stuaus.Children[ index ] as RadioButton;
				return rdb.IsChecked;
			}
		}
			

		/// <summary>
		/// 定时器中执行委托用于显示实时情况
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void UpdateWork(object sender, ElapsedEventArgs e)
		{
			try {
				if (!cts.IsCancellationRequested) {
					object obj;
					for (int index = 0; index < PationOnoffStatus.Length; index++) {
						obj = Dispatcher.Invoke( new dlg_CheckedstatusGet( CheckedstatusGet ), index, wrpOnoff_stuaus, true );
						PationOnoffStatus[ index ] = ( bool )obj;
					}

					for (int index = 0; index < PationErrorStatus.Length; index++) {
						obj = Dispatcher.Invoke( new dlg_CheckedstatusGet( CheckedstatusGet ), index, wrpHidden_stuaus, true );
						PationHiddenStatus[ index ] = ( bool )obj;
					}

					for (int index = 0; index < PationErrorStatus.Length; index++) {
						obj = Dispatcher.Invoke( new dlg_CheckedstatusGet( CheckedstatusGet ), index, wrpError_stuaus, true );
						PationErrorStatus[ index ] = ( bool )obj;
					}

					for (int index = 0; index < PationRegisterStatus.Length; index++) {
						obj = Dispatcher.Invoke( new dlg_CheckedstatusGet( CheckedstatusGet ), index, wrpReigster_stuaus, true );
						PationRegisterStatus[ index ] = ( bool )obj;
					}

					for (int index = 0; index < PationSpeakerErrorStatus.Length; index++) {
						obj = Dispatcher.Invoke( new dlg_CheckedstatusGet( CheckedstatusGet ), index, wrpSpeakerError_stuaus, true );
						PationSpeakerErrorStatus[ index ] = ( bool )obj;
					}

					for (int index = 0; index < AmpRegisterStatus.Length; index++) {
						obj = Dispatcher.Invoke( new dlg_CheckedstatusGet( CheckedstatusGet ), index, wrpAmpResigster_stuaus, true );
						AmpRegisterStatus[ index ] = ( bool )obj;
					}

					for (int index = 0; index < AmpHiddenStatus.Length; index++) {
						obj = Dispatcher.Invoke( new dlg_CheckedstatusGet( CheckedstatusGet ), index, wrpAmpHidden_stuaus, true );
						AmpHiddenStatus[ index ] = ( bool )obj;
					}

					for (int index = 0; index < AmpHiddenStatus.Length; index++) {
						obj = Dispatcher.Invoke( new dlg_CheckedstatusGet( CheckedstatusGet ), index, wrpAmpSetting_stuaus, false );
						if (( bool )obj) {
							for (int index_1 = 21; index_1 < 25; index_1++) {
								obj = Dispatcher.Invoke( new dlg_CheckedstatusGet( CheckedstatusGet ), index_1, wrpAmpSetting_stuaus, true );
								AmpWorkingStatus[ index, (index_1 - 21) ] = ( bool )obj;
							}
						}
					}

					//以下进行串口数据的应答操作
					if((serialPort.BytesToRead != 0) && (last_received_count == serialPort.BytesToRead)) {
						CheckReceivedData();
					}
					last_received_count = serialPort.BytesToRead;
				}
			} catch (Exception ex) {
//				MessageBox.Show( ex.ToString() );
			}
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if(timer != null) { timer.Enabled = false; }

			if(serialPort != null) { serialPort.Close();serialPort.Dispose(); }
			cts.Cancel();
		}

<<<<<<< HEAD
		delegate object dlg_txtValueGet( TextBox textBox );
		delegate void dlg_txtValueShow( TextBox textBox, string value );
		void TxtValueShow( TextBox textBox, string value )
		{
			textBox.Text = value;
		}

		object TxtValueGet(TextBox textBox )
		{
			int value = 0;
			try {
				value = Convert.ToUInt16( textBox.Text );
			} catch {
				value = 0;
			}
			return value;
		}

=======
>>>>>>> parent of 50093c9... 11
		private void CheckReceivedData()
		{
			byte[] temp = new byte[serialPort.BytesToRead];
			serialPort.Read( temp, 0, temp.Length );
			int index_of_header = 0;
			for(index_of_header = 0; index_of_header < temp.Length; index_of_header++) {
				if(temp[ index_of_header] == 0x68) {
					break;
				}
			}

			if(temp.Length < (index_of_header + 3)) { return; }

			byte[] received_data = new byte[ temp[ index_of_header + 3 ] + 6 ];
			int max_count = received_data.Length;
			if((temp.Length - index_of_header) < received_data.Length) {
				max_count = (temp.Length - index_of_header);
			}
			Buffer.BlockCopy( temp, index_of_header, received_data, 0, max_count );

<<<<<<< HEAD
			byte [ ] send_data = new byte [ 20 ];
			send_data [ 0 ] = 0x68;					

			bool no_answer = false; //标记是否需要应答

			switch (( Soundsource.Soundsource_cmd )received_data[ 1 ]) {
				case Soundsource.Soundsource_cmd.Cmd_EmergencyControl:
					Dispatcher.Invoke ( new dlg_txtValueShow ( TxtValueShow ), txtEmergencyStatus, received_data [ 4 ].ToString ( ) );

					common_status &= 0xFFFC;
					common_status |= received_data[ 4 ];
					Dispatcher.Invoke( new dlg_txtValueShow( TxtValueShow ), txtCommonStatus, common_status.ToString("X") );

					send_data [ 1 ] = 0xff;
					send_data [ 3 ] = 0x01;
					send_data [ 4 ] = 0x00;
					break;
				case Soundsource.Soundsource_cmd.Cmd_PationOnoffControl:
					bool[] temp_1 = new bool[ 32 ];
					for (int byte_index = 6; byte_index < 10; byte_index++) {
						for (int bit_index = 0; bit_index < 8; bit_index++) {
							if ((received_data[ byte_index ] & (0x01 << bit_index)) != 0) {
								temp_1[ (byte_index - 6) * 8 + bit_index ] = true;
							}
						}
					}
					//将数组数据提取到对应的有效数组中，并在控件中显示
					Buffer.BlockCopy( temp_1, 0, PationOnoffStatus, received_data[ 4 ], (received_data[ 5 ] - received_data[ 4 ] + 1) );
					for(int index = received_data[4];index <= received_data[ 5 ]; index++) {
						if (PationOnoffStatus[ index ]) {
							Dispatcher.Invoke( new dlg_CheckedstatusSet( CheckedstatusSet ), index , wrpOnoff_stuaus, true, true );
						} else {
							Dispatcher.Invoke( new dlg_CheckedstatusSet( CheckedstatusSet ), index , wrpOnoff_stuaus, false, true );
						}
					}
					send_data[ 1 ] = 0xff;
					send_data[ 3 ] = 0x01;
					send_data[ 4 ] = 0x00;
=======
			switch (( Soundsource.soundsource_cmd )received_data[ 3 ]) {
				case Soundsource.soundsource_cmd.Cmd_EmergencyControl:
					break;
				case Soundsource.soundsource_cmd.Cmd_PationOnoffControl:
>>>>>>> parent of 50093c9... 11
					break;
				case Soundsource.Soundsource_cmd.Cmd_PationHiddenControl:
					temp_1 = new bool[ 32 ];
					for (int byte_index = 6; byte_index < 10; byte_index++) {
						for (int bit_index = 0; bit_index < 8; bit_index++) {
							if ((received_data[ byte_index ] & (0x01 << bit_index)) != 0) {
								temp_1[ (byte_index - 6) * 8 + bit_index ] = true;
							}
						}
					}
					//将数组数据提取到对应的有效数组中，并在控件中显示
					Buffer.BlockCopy( temp_1, 0, PationHiddenStatus, received_data[ 4 ], (received_data[ 5 ] - received_data[ 4 ] + 1) );
					for (int index = received_data[ 4 ]; index <= received_data[ 5 ]; index++) {
						if (PationHiddenStatus[ index ]) {
							Dispatcher.Invoke( new dlg_CheckedstatusSet( CheckedstatusSet ), index , wrpHidden_stuaus, true, true );
						} else {
							Dispatcher.Invoke( new dlg_CheckedstatusSet( CheckedstatusSet ), index , wrpHidden_stuaus, false, true );
						}
					}
					send_data[ 1 ] = 0xff;
					send_data[ 3 ] = 0x01;
					send_data[ 4 ] = 0x00;
					break;
				case Soundsource.Soundsource_cmd.Cmd_PationOnoffQuery:
					int temp_2 = 9;
					int temp_3 = 0;
					int max_index = received_data[ 5 ];
					if((received_data[5] - received_data[4] ) > 31) {
						max_index = received_data[ 4 ] + 31;
					}
					for (int bit_index = received_data[ 4 ]; bit_index <= max_index; bit_index++) {						
						if (PationOnoffStatus[ bit_index ]) {
							send_data[ temp_2 ] |= ( byte )(0x01 << temp_3);
						} else {
							send_data[ temp_2 ] &= ( byte )(~(0x01 << temp_3));
						}
						if( ++temp_3 >= 8) {
							temp_3 = 0;
							temp_2--;
						}
					}
					send_data[ 1 ] = received_data[1];
					send_data[ 3 ] = 0x06;
					send_data[ 4 ] = received_data[4];
					send_data[ 5 ] = received_data[5];
					break;
				case Soundsource.Soundsource_cmd.Cmd_PationHiddenQuery:
					temp_2 = 9;
					temp_3 = 0;
					max_index = received_data[ 5 ];
					if ((received_data[ 5 ] - received_data[ 4 ]) > 31) {
						max_index = received_data[ 4 ] + 31;
					}
					for (int bit_index = received_data[ 4 ]; bit_index <= max_index; bit_index++) {
						if (PationHiddenStatus[ bit_index ]) {
							send_data[ temp_2 ] |= ( byte )(0x01 << temp_3);
						} else {
							send_data[ temp_2 ] &= ( byte )(~(0x01 << temp_3));
						}
						if (++temp_3 >= 8) {
							temp_3 = 0;
							temp_2--;
						}
					}
					send_data[ 1 ] = received_data[ 1 ];
					send_data[ 3 ] = 0x06;
					send_data[ 4 ] = received_data[ 4 ];
					send_data[ 5 ] = received_data[ 5 ];
					break;
				case Soundsource.Soundsource_cmd.Cmd_PationErrorQuery:
					temp_2 = 9;
					temp_3 = 0;
					max_index = received_data[ 5 ];
					if ((received_data[ 5 ] - received_data[ 4 ]) > 31) {
						max_index = received_data[ 4 ] + 31;
					}
					for (int bit_index = received_data[ 4 ]; bit_index <= max_index; bit_index++) {
						if (PationErrorStatus[ bit_index ]) {
							send_data[ temp_2 ] |= ( byte )(0x01 << temp_3);
						} else {
							send_data[ temp_2 ] &= ( byte )(~(0x01 << temp_3));
						}
						if (++temp_3 >= 8) {
							temp_3 = 0;
							temp_2--;
						}
					}
					send_data[ 1 ] = received_data[ 1 ];
					send_data[ 3 ] = 0x06;
					send_data[ 4 ] = received_data[ 4 ];
					send_data[ 5 ] = received_data[ 5 ];
					break;
				case Soundsource.Soundsource_cmd.Cmd_BeepErase:
					Dispatcher.Invoke( new dlg_txtValueShow( TxtValueShow ), txtBeepEraseStatus, received_data[ 4 ].ToString() );

					common_status &= 0xFFFB;
					byte value =  (byte)(received_data[ 4 ] & 0x01);
					value <<= 2;
					common_status |= value;
					Dispatcher.Invoke( new dlg_txtValueShow( TxtValueShow ), txtCommonStatus, common_status.ToString( "X" ) );

					send_data[ 1 ] = 0xff;
					send_data[ 3 ] = 0x01;
					send_data[ 4 ] = 0x00;
					break;
				case Soundsource.Soundsource_cmd.Cmd_PationAllControl:
					Dispatcher.Invoke( new dlg_txtValueShow( TxtValueShow ), txtAllStatus, received_data[ 4 ].ToString() );

					common_status &= 0xFFE7;
					value = ( byte )(received_data[ 4 ] & 0x03);
					value <<= 3;
					common_status |= value;
					Dispatcher.Invoke( new dlg_txtValueShow( TxtValueShow ), txtCommonStatus, common_status.ToString( "X" ) );

					send_data[ 1 ] = 0xff;
					send_data[ 3 ] = 0x01;
					send_data[ 4 ] = 0x00;
					break;
				case Soundsource.Soundsource_cmd.Cmd_PationErrorControl:
					temp_1 = new bool[ 32 ];
					for (int byte_index = 6; byte_index < 10; byte_index++) {
						for (int bit_index = 0; bit_index < 8; bit_index++) {
							if ((received_data[ byte_index ] & (0x01 << bit_index)) != 0) {
								temp_1[ (byte_index - 6) * 8 + bit_index ] = true;
							}
						}
					}
					//将数组数据提取到对应的有效数组中，并在控件中显示
					Buffer.BlockCopy( temp_1, 0, PationErrorStatus, received_data[ 4 ], (received_data[ 5 ] - received_data[ 4 ] + 1) );
					for (int index = received_data[ 4 ]; index <= received_data[ 5 ]; index++) {
						if (PationErrorStatus[ index ]) {
							Dispatcher.Invoke( new dlg_CheckedstatusSet( CheckedstatusSet ), index, wrpError_stuaus, true, true );
						} else {
							Dispatcher.Invoke( new dlg_CheckedstatusSet( CheckedstatusSet ), index , wrpError_stuaus, false, true );
						}
					}
					send_data[ 1 ] = 0xff;
					send_data[ 3 ] = 0x01;
					send_data[ 4 ] = 0x00;
					break;
				case Soundsource.Soundsource_cmd.Cmd_FlashAddressSet:
					send_data[ 1 ] = 0xff;
					send_data[ 3 ] = 0x01;
					send_data[ 4 ] = 0x00;
					break;
				case Soundsource.Soundsource_cmd.Cmd_FlashDataWrite:
					send_data[ 1 ] = 0xff;
					send_data[ 3 ] = 0x01;
					send_data[ 4 ] = 0x00;
					break;
				case Soundsource.Soundsource_cmd.Cmd_FlashDataRead:
					send_data[ 1 ] = received_data[1];
					send_data[ 3 ] = 16;
					for(int index_1=4;index_1 < 20; index_1++) {
						send_data[ index_1 ] = (byte)(index_1 - 3);
					}
					break;
				case Soundsource.Soundsource_cmd.Cmd_WorkingStatusQuery:					
				 	object obj = Dispatcher.Invoke( new dlg_txtValueGet( TxtValueGet ), txtCommonStatus);
					send_data[ 1 ] = received_data[1];
					send_data[ 3 ] = 2;
					byte[] bytes = BitConverter.GetBytes( Convert.ToUInt16( obj ) );

					send_data[ 4 ] = bytes[ 0 ];
					send_data[ 5 ] = bytes[ 1 ];
					break;
				case Soundsource.Soundsource_cmd.Cmd_PationAddressSet:

					Dispatcher.Invoke( new dlg_txtValueShow( TxtValueShow ), txtMainpointAddress, received_data[ 4 ].ToString() );
					Dispatcher.Invoke( new dlg_txtValueShow( TxtValueShow ), txtPationStartAddress, received_data[ 5 ].ToString() );
					Dispatcher.Invoke( new dlg_txtValueShow( TxtValueShow ), txtPationEndAddress, received_data[ 6 ].ToString() );

					send_data[ 1 ] = 0xff;
					send_data[ 3 ] = 0x01;
					send_data[ 4 ] = 0x00;
					break;
				case Soundsource.Soundsource_cmd.Cmd_PationWatchRightsSet:
					value = received_data[ 4 ];
					Dispatcher.Invoke( new dlg_txtValueShow( TxtValueShow ), txtAdditionalStatus, ((value & 0x04) >> 2).ToString() );
					value = received_data[ 4 ];

					common_status &= 0xFF9F;
					value = ( byte )(received_data[ 4 ] & 0x03);
					value <<= 5;
					common_status |= value;
					Dispatcher.Invoke( new dlg_txtValueShow( TxtValueShow ), txtCommonStatus, common_status.ToString( "X" ) );

					send_data[ 1 ] = 0xff;
					send_data[ 3 ] = 0x01;
					send_data[ 4 ] = 0x00;
					break;
				case Soundsource.Soundsource_cmd.Cmd_SelfCheck:
					Dispatcher.Invoke( new dlg_txtValueShow( TxtValueShow ), txtSelfCheckStatus, received_data[ 4 ].ToString() );
					send_data[ 1 ] = 0xff;
					send_data[ 3 ] = 0x01;
					send_data[ 4 ] = 0x00;
					break;
				case Soundsource.Soundsource_cmd.Cmd_PationRegisterControl:
					temp_1 = new bool[ 32 ];
					for (int byte_index = 6; byte_index < 10; byte_index++) {
						for (int bit_index = 0; bit_index < 8; bit_index++) {
							if ((received_data[ byte_index ] & (0x01 << bit_index)) != 0) {
								temp_1[ (byte_index - 6) * 8 + bit_index ] = true;
							}
						}
					}
					//将数组数据提取到对应的有效数组中，并在控件中显示
					Buffer.BlockCopy( temp_1, 0, PationRegisterStatus, received_data[ 4 ], (received_data[ 5 ] - received_data[ 4 ] + 1) );
					for (int index = received_data[ 4 ]; index <= received_data[ 5 ]; index++) {
						if (PationRegisterStatus[ index  ]) {
							Dispatcher.Invoke( new dlg_CheckedstatusSet( CheckedstatusSet ), index , wrpReigster_stuaus, true, true );
						} else {
							Dispatcher.Invoke( new dlg_CheckedstatusSet( CheckedstatusSet ), index , wrpReigster_stuaus, false, true );
						}
					}
					send_data[ 1 ] = 0xff;
					send_data[ 3 ] = 0x01;
					send_data[ 4 ] = 0x00;
					break;
				case Soundsource.Soundsource_cmd.Cmd_PationRegisterQuery:
					temp_2 = 9;
					temp_3 = 0;
					max_index = received_data[ 5 ];
					if ((received_data[ 5 ] - received_data[ 4 ]) > 31) {
						max_index = received_data[ 4 ] + 31;
					}
					for (int bit_index = received_data[ 4 ]; bit_index <= max_index; bit_index++) {
						if (PationRegisterStatus[ bit_index ]) {
							send_data[ temp_2 ] |= ( byte )(0x01 << temp_3);
						} else {
							send_data[ temp_2 ] &= ( byte )(~(0x01 << temp_3));
						}
						if (++temp_3 >= 8) {
							temp_3 = 0;
							temp_2--;
						}
					}
					send_data[ 1 ] = received_data[ 1 ];
					send_data[ 3 ] = 0x06;
					send_data[ 4 ] = received_data[ 4 ];
					send_data[ 5 ] = received_data[ 5 ];
					break;
				case Soundsource.Soundsource_cmd.Cmd_SpeakerErrorQuery:
					temp_2 = 9;
					temp_3 = 0;
					max_index = received_data[ 5 ];
					if ((received_data[ 5 ] - received_data[ 4 ]) > 31) {
						max_index = received_data[ 4 ] + 31;
					}
					for (int bit_index = received_data[ 4 ]; bit_index <= max_index; bit_index++) {
						if (PationSpeakerErrorStatus[ bit_index ]) {
							send_data[ temp_2 ] |= ( byte )(0x01 << temp_3);
						} else {
							send_data[ temp_2 ] &= ( byte )(~(0x01 << temp_3));
						}
						if (++temp_3 >= 8) {
							temp_3 = 0;
							temp_2--;
						}
					}
					send_data[ 1 ] = received_data[ 1 ];
					send_data[ 3 ] = 0x06;
					send_data[ 4 ] = received_data[ 4 ];
					send_data[ 5 ] = received_data[ 5 ];
					break;
				case Soundsource.Soundsource_cmd.Cmd_SpeakerErrorControl:
					temp_1 = new bool[ 32 ];
					for (int byte_index = 6; byte_index < 10; byte_index++) {
						for (int bit_index = 0; bit_index < 8; bit_index++) {
							if ((received_data[ byte_index ] & (0x01 << bit_index)) != 0) {
								temp_1[ (byte_index - 6) * 8 + bit_index ] = true;
							}
						}
					}
					//将数组数据提取到对应的有效数组中，并在控件中显示
					Buffer.BlockCopy( temp_1, 0, PationSpeakerErrorStatus, received_data[ 4 ], (received_data[ 5 ] - received_data[ 4 ] + 1));
					for (int index = received_data[ 4 ]; index <= received_data[ 5 ]; index++) {
						if (PationSpeakerErrorStatus[ index ]) {
							Dispatcher.Invoke( new dlg_CheckedstatusSet( CheckedstatusSet ), index , wrpSpeakerError_stuaus, true, true );
						} else {
							Dispatcher.Invoke( new dlg_CheckedstatusSet( CheckedstatusSet ), index , wrpSpeakerError_stuaus, false, true );
						}
					}
					send_data[ 1 ] = 0xff;
					send_data[ 3 ] = 0x01;
					send_data[ 4 ] = 0x00;
					break;
				case Soundsource.Soundsource_cmd.Cmd_Reset:
					Dispatcher.Invoke( new dlg_txtValueShow( TxtValueShow ), txtResetStatus, received_data[ 4 ].ToString() );
					send_data[ 1 ] = 0xff;
					send_data[ 3 ] = 0x01;
					send_data[ 4 ] = 0x00;
					break;
				case Soundsource.Soundsource_cmd.Cmd_AmpStatusQuery:
					byte amp_working_status = 0;
					for(int bit_index = 0;bit_index < 4; bit_index++) {
						if (AmpWorkingStatus[ received_data[ 4 ], bit_index ]) {
							amp_working_status |= (byte)(0x01 << bit_index);
						}
					}
					send_data[ 1 ] = received_data[ 1 ];
					send_data[ 3 ] = 0x01;
					send_data[ 4 ] = amp_working_status;
					break;
				case Soundsource.Soundsource_cmd.Cmd_BaudrateControl:
					value = received_data[ 4 ];				
					if (value == 0) {
						serialPort.BaudRate = 4800;
					} else if (value == 1) {
						serialPort.BaudRate = 9600;
					} else if (value == 2) {
						serialPort.BaudRate = 19200;
					} else if (value == 3) {
						serialPort.BaudRate = 57600;
					}else if(value == 4) {
						serialPort.BaudRate = 115200;
					}else if(value == 5) {
						serialPort.BaudRate = 460800;
					}
					Dispatcher.Invoke( new dlg_txtValueShow( TxtValueShow ), txtBaudrate, serialPort.BaudRate.ToString() );
					break;
				case Soundsource.Soundsource_cmd.Cmd_AmpRegisterControl:
					temp_1 = new bool[ 32 ];
					for (int byte_index = 6; byte_index < 10; byte_index++) {
						for (int bit_index = 0; bit_index < 8; bit_index++) {
							if ((received_data[ byte_index ] & (0x01 << bit_index)) != 0) {
								temp_1[ (byte_index - 6) * 8 + bit_index ] = true;
							}
						}
					}
					//将数组数据提取到对应的有效数组中，并在控件中显示
					Buffer.BlockCopy( temp_1, 0, AmpRegisterStatus, received_data[ 4 ], (received_data[ 5 ] - received_data[ 4 ] + 1) );
					for (int index = received_data[ 4 ]; index <= received_data[ 5 ]; index++) {
						if (AmpRegisterStatus[ index ]) {
							Dispatcher.Invoke( new dlg_CheckedstatusSet( CheckedstatusSet ), index , wrpAmpResigster_stuaus, true, true );
						} else {
							Dispatcher.Invoke( new dlg_CheckedstatusSet( CheckedstatusSet ), index , wrpAmpResigster_stuaus, false, true );
						}
					}
					send_data[ 1 ] = 0xff;
					send_data[ 3 ] = 0x01;
					send_data[ 4 ] = 0x00;
					break;
				case Soundsource.Soundsource_cmd.Cmd_AmpRegisterQuery:
					temp_2 = 9;
					temp_3 = 0;
					max_index = received_data[ 5 ];
					if ((received_data[ 5 ] - received_data[ 4 ]) > 20) {
						max_index = received_data[ 4 ] + 20;
					}
					for (int bit_index = received_data[ 4 ]; bit_index <= max_index; bit_index++) {
						if (AmpRegisterStatus[ bit_index ]) {
							send_data[ temp_2 ] |= ( byte )(0x01 << temp_3);
						} else {
							send_data[ temp_2 ] &= ( byte )(~(0x01 << temp_3));
						}
						if (++temp_3 >= 8) {
							temp_3 = 0;
							temp_2--;
						}
					}
					send_data[ 1 ] = received_data[ 1 ];
					send_data[ 3 ] = 0x06;
					send_data[ 4 ] = received_data[ 4 ];
					send_data[ 5 ] = received_data[ 5 ];
					break;
				case Soundsource.Soundsource_cmd.Cmd_AmpHiddenControl:
					temp_1 = new bool[ 32 ];
					for (int byte_index = 6; byte_index < 10; byte_index++) {
						for (int bit_index = 0; bit_index < 8; bit_index++) {
							if ((received_data[ byte_index ] & (0x01 << bit_index)) != 0) {
								temp_1[ (byte_index - 6) * 8 + bit_index ] = true;
							}
						}
					}
					//将数组数据提取到对应的有效数组中，并在控件中显示
					Buffer.BlockCopy( temp_1, 0, AmpHiddenStatus, received_data[ 4 ], (received_data[ 5 ] - received_data[ 4 ]+ 1) );
					for (int index = received_data[ 4 ]; index <= received_data[ 5 ]; index++) {
						if (AmpHiddenStatus[ index ]) {
							Dispatcher.Invoke( new dlg_CheckedstatusSet( CheckedstatusSet ), index , wrpAmpHidden_stuaus, true, true );
						} else {
							Dispatcher.Invoke( new dlg_CheckedstatusSet( CheckedstatusSet ), index , wrpAmpHidden_stuaus, false, true );
						}
					}
					send_data[ 1 ] = 0xff;
					send_data[ 3 ] = 0x01;
					send_data[ 4 ] = 0x00;
					break;
				case Soundsource.Soundsource_cmd.Cmd_AmpHiddenQuery:
					temp_2 = 9;
					temp_3 = 0;
					max_index = received_data[ 5 ];
					if ((received_data[ 5 ] - received_data[ 4 ]) > 20) {
						max_index = received_data[ 4 ] + 20;
					}
					for (int bit_index = received_data[ 4 ]; bit_index <= max_index; bit_index++) {
						if (AmpHiddenStatus[ bit_index ]) {
							send_data[ temp_2 ] |= ( byte )(0x01 << temp_3);
						} else {
							send_data[ temp_2 ] &= ( byte )(~(0x01 << temp_3));
						}
						if (++temp_3 >= 8) {
							temp_3 = 0;
							temp_2--;
						}
					}
					send_data[ 1 ] = received_data[ 1 ];
					send_data[ 3 ] = 0x06;
					send_data[ 4 ] = received_data[ 4 ];
					send_data[ 5 ] = received_data[ 5 ];
					break;
				default:
					no_answer = true;
					break;
			}

			if (!no_answer) {
				byte[] real_sent = new byte[ send_data[ 3 ] + 6 ];
				send_data[ 0 ] = 0x68;
				send_data[ 2 ] = Convert.ToByte( 0xff - send_data[ 1 ] );

				Buffer.BlockCopy( send_data, 0, real_sent, 0, send_data[ 3 ] + 4 );
				real_sent[real_sent[3] + 4] = Calibrate( send_data );
				real_sent[ real_sent[ 3 ] + 5 ] = 0x16;

				serialPort.Write( real_sent, 0, real_sent.Length );  //实际发送

				//int temp11 = real_sent.Length;
				//for (int temp1 = 0; temp1 < temp11; temp1++) {
				//	byte[] temp2 = new byte[ 1 ];
				//	Buffer.BlockCopy( real_sent, temp1, temp2, 0, 1 );
				//	serialPort.Write( temp2, 0, 1 );  //实际发送 - 单字节发送
				//}
			}

		}

		private byte Calibrate(byte[] send_data)
		{
			ushort value = 0;
			for(int index = 4;index < (send_data[3] +4); index++) {
				value += send_data[ index ];
			}

			value &= 0x00FF;
			return Convert.ToByte( value );
		}

	}
}
