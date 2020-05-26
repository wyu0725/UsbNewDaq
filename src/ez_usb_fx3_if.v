`timescale 1ns/1ps

module ez_usb_fx3_if(
  //clk and rst
  input                            clk,
  input                            rst,

  //ez usb interface
  output                           ez_usb_pclk, // usb interface clk
  output                           ez_usb_reset_n,// usb reset

  output [1:0]                      ez_usb_addr, // usb fifo addr
  // usb data bus; dio = dout_t ? din : dout 
  input [31:0]                     ez_usb_din,
  output reg [31:0]                ez_usb_dout = 32'b0,
  output reg                       ez_usb_dout_t = 1'b1, //A logic High on the T pin disables the output buffer

  input                            ez_usb_flaga, //addr 00, fpga -> usb, full flag, 1 = not full  0 = full
  input                            ez_usb_flagb, //addr 01, usb -> fpga, empty flag, 1 = not empty 0 = empty
  input                            ez_usb_flagc, //addr 10, fpga -> usb, full flag, 1 = not full  0 = full
  input                            ez_usb_flagd, //addr 11, usb -> fpga, empty flag, 1 = not empty 0 = empty

  output                           ez_usb_slcs_n, // usb chip select, active low
  output                           ez_usb_slrd_n, // usb read enable, active low
  output                           ez_usb_sloe_n, // usb data bus output enable, this enable data from usb -> fpga
  output                           ez_usb_slwr_n, // usb write enable
  output                           ez_usb_pktend_n, // fpga -> usb packet end

  //user interface
  //fpga --> usb
  input       [31:0]               s_axis_tdata,
  input                            s_axis_tvalid,
  input                            s_axis_tlast,
  output  reg                      s_axis_tready = 1'b0,
  //usb --> fpga
  input                            m_axis_tready,
  output  reg    [31:0]            m_axis_tdata = 32'h0,
  output  reg                      m_axis_tvalid = 1'b0
  );

  //---------------------------------------------------------------------------
  //3 cycle latency from slwr to flag 
  //3 cycle latency from addr to data
  //2 cycle latency from slrd to data
  //---------------------------------------------------------------------------

  //we need at least 5 clk cycle to read a data from usb to fpga
  //3 clk cycle to wait data stable on usb bus
  //1 clk cycle to hold data on user interface
  //at least 1 clk to wait user fetch data
  //
  //we need 2 clk cycle to write a data from fpga to usb
  //1 clk to wait fifo addr and pre-fetch data from user interface
  //1 clk to assert slwr
  localparam IDLE          = 9'b000000001;
  localparam READ_RD       = 9'b000000010;
  localparam READ_DELAY    = 9'b000000100;
  localparam READ_START    = 9'b000001000;
  localparam READ_DATA     = 9'b000010000;
  localparam WRITE_DATA    = 9'b000100000;
  localparam WRITE_DELAY1  = 9'b001000000;
  localparam WRITE_DELAY2  = 9'b010000000;
  localparam WRITE_DELAY3  = 9'b100000000;
  reg [8:0] usb_state      = IDLE;
  reg [8:0] usb_next_state = IDLE;

  reg sloe_n;
  reg slrd_n;
  reg slwr_n;
  reg pktend_n;
  reg [1:0] addr;

  //---------------------------------------------------------------------------
  //there will be 3 clk delay from slwr to flag, and the buffer size is 1024B
  //for usb 3.0, or 512B for usb 2.0; although for streaming mode, the buffer
  //size is 16kB for usb 3.0, or 8kB for usb 2.0;
  //so we maintain a counter, every 128 data cycle(128*32bit = 512B), 
  //we delay 3 clk cycle to wait flag
  //in this way, we can guarantee that we will fullfill the buffer 
  //---------------------------------------------------------------------------
  reg [7:0] write_cnt = 8'h00;

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
        if(ez_usb_flagc && m_axis_tready)
          usb_next_state = READ_RD;
        else if (ez_usb_flaga && s_axis_tvalid)
          usb_next_state = WRITE_DATA;
        else
          usb_next_state = IDLE;
      end

      READ_RD: usb_next_state = READ_DELAY;

      READ_DELAY: usb_next_state = READ_START;

      READ_START: usb_next_state = READ_DATA;

      READ_DATA: begin
        if( m_axis_tready && m_axis_tvalid)
          usb_next_state = IDLE;
        else
          usb_next_state = READ_DATA;
      end

      WRITE_DATA: begin
        if(write_cnt[7] || s_axis_tlast)
          usb_next_state = WRITE_DELAY1;
        else
          usb_next_state = IDLE;
      end
      WRITE_DELAY1: usb_next_state = WRITE_DELAY2;
      WRITE_DELAY2: usb_next_state = WRITE_DELAY3;
      WRITE_DELAY3: usb_next_state = IDLE;

      default:
        usb_next_state = IDLE;
    endcase
  end

  //---------------------------------------------------------------------------
  //ez usb gpif ii interface
  //when usb -> fpga, addr must stable before sloe, or there will be one more
  //data 32'h0
  //---------------------------------------------------------------------------
  always @(*) begin
    case(usb_state)
      IDLE: begin
        if(usb_next_state == READ_RD) begin
          addr    = 2'b11;
          sloe_n  = 1'b0;
        end
        else if( usb_next_state == WRITE_DATA) begin
          addr    = 2'b00;
          sloe_n  = 1'b1;
        end
        else begin
          addr    = 2'b11;  // default address must be bulk out addr, or it need one clk cycle to switch addr
          sloe_n  = 1'b1;
        end

        slrd_n    = 1'b1;

        slwr_n    = 1'b1;
        pktend_n  = 1'b1;
        
      end
      READ_RD:begin
        sloe_n    = 1'b0;
        addr      = 2'b11;
        slrd_n    = 1'b0;

        slwr_n    = 1'b1;
        pktend_n  = 1'b1;
      end
      READ_DELAY:begin
        sloe_n    = 1'b0;
        addr      = 2'b11;
        slrd_n    = 1'b1;

        slwr_n    = 1'b1;
        pktend_n  = 1'b1;
      end
      READ_START:begin
        sloe_n    = 1'b0;
        addr      = 2'b11;
        slrd_n    = 1'b1;

        slwr_n    = 1'b1;
        pktend_n  = 1'b1;
      end
      READ_DATA:begin
        sloe_n    = 1'b0;
        addr      = 2'b11;
        slrd_n    = 1'b1;

        slwr_n    = 1'b1;
        pktend_n  = 1'b1;
      end

      WRITE_DATA:begin
        addr      = 2'b00;
        slwr_n    = 1'b0;
        pktend_n  = !s_axis_tlast;

        sloe_n    = 1'b1;
        slrd_n    = 1'b1;
      end 

      WRITE_DELAY1:begin
        addr      = 2'b00;
        slwr_n    = 1'b1;
        pktend_n  = 1'b1;

        sloe_n    = 1'b1;
        slrd_n    = 1'b1;
      end 

      WRITE_DELAY2:begin
        addr      = 2'b00;
        slwr_n    = 1'b1;
        pktend_n  = 1'b1;

        sloe_n    = 1'b1;
        slrd_n    = 1'b1;
      end 

      WRITE_DELAY3:begin
        addr      = 2'b00;
        slwr_n    = 1'b1;
        pktend_n  = 1'b1;

        sloe_n    = 1'b1;
        slrd_n    = 1'b1;
      end 

      default: begin
        sloe_n    = 1'b1;
        slrd_n    = 1'b1;
        slwr_n    = 1'b1;
        addr      = 2'b11;
        pktend_n  = 1'b1;
      end
    endcase
  end

  //---------------------------------------------------------------------------
  //
  //---------------------------------------------------------------------------
  always @(posedge clk) begin
    if(rst) begin
      m_axis_tdata  <= 32'h0000;
      m_axis_tvalid <= 1'b0;
    end
    else begin 
      if( usb_state== READ_START) begin
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
      ez_usb_dout <= 32'h0;
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
  
  //---------------------------------------------------------------------------
  //
  //---------------------------------------------------------------------------
  always @(posedge clk) begin
    if(rst) begin
      write_cnt <= 8'h00;
    end
    else begin
      if(usb_next_state == WRITE_DATA && s_axis_tlast)
        write_cnt <= 8'h00;
      else if(usb_next_state == WRITE_DATA)
        write_cnt <= write_cnt + 1'b1;
      else if(write_cnt[7])
        write_cnt <= 8'h00;
    end
  end

  assign ez_usb_slcs_n   = 1'b0;
  assign ez_usb_sloe_n   = sloe_n;
  assign ez_usb_slrd_n   = slrd_n;
  assign ez_usb_slwr_n   = slwr_n;
  assign ez_usb_pktend_n = pktend_n;
  assign ez_usb_addr     = addr;

  assign ez_usb_pclk = ~clk;
  assign ez_usb_reset_n = ~rst;
  
endmodule
