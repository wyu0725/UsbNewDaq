`timescale 1ns/1ps

module ez_usb_fx2lp_if(
  //clk and rst
  input             clk,
  input             rst,
  //---------------------------------------------------------------------------
  //ez usb interface
  //---------------------------------------------------------------------------
  output            ez_usb_ifclk,         // usb interface clk
  // usb reset_n pin, must be asserted for at least 5 ms.
  output            ez_usb_reset_n,       // usb reset
  output [1:0]      ez_usb_addr,          // usb fifo addr
  // usb data bus  dio = dout_t ? din : dout
  input [15:0]      ez_usb_din,
  output reg [15:0] ez_usb_dout = 16'b0,
  output reg        ez_usb_dout_t = 1'b1, // A logic High on the T pin disables the output buffer

  input             ez_usb_flaga,         // Not used
  input             ez_usb_flagb,         // addr 10, fpga -> usb, full flag, 1 = not full  0 = full
  input             ez_usb_flagc,         // addr 00, usb -> fpga, empty flag, 1 = empty 0 = not empty
  // input ez_usb_flagd,// addr 11, usb -> fpga, empty flag, 1 = not empty 0 = empty
  output            ez_usb_slcs_n,        // usb chip select, active low
  output            ez_usb_slrd_n,        // usb read enable, active low
  output            ez_usb_sloe_n,        // usb data bus output enable, this enable data from usb -> fpga
  output            ez_usb_slwr_n,        // usb write enable
  output            ez_usb_pktend_n,      // fpga -> usb packet end
  // user interface
  // fpga --> usb
  input [15:0]      s_axis_tdata,         // 16 bits data to USB
  input             s_axis_tvalid,        // Data to usb valid
  output reg        s_axis_tready = 1'b0, // USB ready to receive data. High indicates that the data is received
  input             s_axis_tlast,         // A pulse indicates that the last package is sent
  // usb --> fpga
  input             m_axis_tready,        // FPGA ready to receive the data from USB
  output reg [15:0] m_axis_tdata = 16'h0, // USB to FPGA 16 bits data
  output reg        m_axis_tvalid = 1'b0  // USB to FPGA data valid
  );

  localparam IDLE          = 6'b000001;
  localparam READ_DELAY    = 6'b000010;// fx2lp fifo addr setup time > one ifclk when ifclk = 48MHz;
  localparam READ_START    = 6'b000100;// fetch data from usb bus
  localparam READ_DATA     = 6'b001000;// wait user to fetch data
  localparam WRITE_DELAY   = 6'b010000;// fx2lp fifo addr setup time > one ifclk when ifclk = 48MHz;
  localparam WRITE_DATA    = 6'b100000;// send data to usb bus
  reg [5:0] usb_state      = IDLE;
  reg [5:0] usb_next_state = IDLE;

  reg sloe_n; 
  reg slrd_n;
  reg slwr_n;
  reg pktend_n;
  reg [1:0] addr;

  //---------------------------------------------------------------------------
  //usb state machine
  //read first
  //single read/write mode
  //---------------------------------------------------------------------------
  always @(posedge clk) begin
    if(rst) usb_state <= IDLE;
    else usb_state <= usb_next_state;
  end

  always @(*) begin
    case(usb_state)
      IDLE:begin
        if(!ez_usb_flagc && m_axis_tready)
          usb_next_state = READ_DELAY;
        else if (!ez_usb_flagb && s_axis_tvalid)
          usb_next_state = WRITE_DELAY;
        else
          usb_next_state = IDLE;
      end

      READ_DELAY: 
        usb_next_state = READ_START;

      READ_START: 
        usb_next_state = READ_DATA;

      READ_DATA:begin
        if (m_axis_tready && m_axis_tvalid)
          usb_next_state = IDLE;
        else
          usb_next_state = READ_DATA;
      end

      WRITE_DELAY: 
        usb_next_state = WRITE_DATA;

      WRITE_DATA:
          usb_next_state = IDLE;

      default:
        usb_next_state = IDLE;
    endcase
  end

  //---------------------------------------------------------------------------
  //ez usb gpif interface
  //---------------------------------------------------------------------------
  always @(*) begin

    case(usb_state)
      IDLE: begin
        if(usb_next_state == READ_DELAY) begin
          addr    = 2'b00;
          sloe_n  = 1'b0;
        end
        else if( usb_next_state == WRITE_DELAY) begin
          addr    = 2'b10;
          sloe_n  = 1'b1;
        end
        else begin
          addr    = 2'b10;
          sloe_n  = 1'b1;
        end

        slrd_n    = 1'b1;

        slwr_n    = 1'b1;
        pktend_n  = 1'b1;
      end
      READ_DELAY: begin
        sloe_n    = 1'b0;
        addr      = 2'b00;
        slrd_n    = 1'b1;

        slwr_n    = 1'b1;
        pktend_n  = 1'b1;
      end
      READ_START:begin
        sloe_n    = 1'b0;
        addr      = 2'b00;
        slrd_n    = 1'b0;

        slwr_n    = 1'b1;
        pktend_n  = 1'b1;
      end
      READ_DATA:begin
        sloe_n    = 1'b0;
        addr      = 2'b00;
        slrd_n    = 1'b1;

        slwr_n    = 1'b1;
        pktend_n  = 1'b1;
      end
      WRITE_DELAY:begin
        addr = 2'b10;
        slwr_n    = 1'b1;
        pktend_n  = 1'b1;

        sloe_n    = 1'b1;
        slrd_n    = 1'b1;
      end
      WRITE_DATA:begin
        addr      = 2'b10;
        slwr_n    = 1'b0;
        pktend_n  = !s_axis_tlast;

        sloe_n    = 1'b1;
        slrd_n    = 1'b1;
      end 
      default: begin
        addr      = 2'b10;
        sloe_n    = 1'b1;
        slrd_n    = 1'b1;
        slwr_n    = 1'b1;
        pktend_n  = 1'b1;
      end
    endcase
  end

  //---------------------------------------------------------------------------
  //
  //---------------------------------------------------------------------------
  always @(posedge clk) begin
    if(rst) begin
      m_axis_tdata  <= 16'h0000;
      m_axis_tvalid <= 1'b0;
    end
    else begin 
      if( usb_state == READ_START) begin
        m_axis_tdata  <= ez_usb_din;
        m_axis_tvalid <= 1'b1;
      end

      if(m_axis_tvalid && m_axis_tready)
        m_axis_tvalid <= 1'b0;
    end
  end

  //---------------------------------------------------------------------------
  //
  //---------------------------------------------------------------------------
  
  always @(posedge clk) begin
    if(rst) begin
      s_axis_tready <= 1'b0;
      ez_usb_dout <= 16'h0;
      ez_usb_dout_t <= 1'b1;
    end
    else if ( usb_next_state == WRITE_DATA ) begin
      s_axis_tready <= 1'b1;
      ez_usb_dout  <= s_axis_tdata;
      ez_usb_dout_t <= 1'b0;
    end
    else begin
      s_axis_tready <= 1'b0;
      ez_usb_dout_t <= 1'b1;
    end
  end

  assign ez_usb_slcs_n   = 1'b0;
  assign ez_usb_sloe_n   = sloe_n;
  assign ez_usb_slrd_n   = slrd_n;
  assign ez_usb_slwr_n   = slwr_n;
  assign ez_usb_pktend_n = pktend_n;
  assign ez_usb_addr     = addr;

  assign ez_usb_ifclk = ~clk;
  assign ez_usb_reset_n = ~rst;
endmodule
