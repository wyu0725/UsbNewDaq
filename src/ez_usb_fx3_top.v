`timescale 1ns/1ps
module ez_usb_fx3_top(
    input clk,
    input rst_n,

    output usb_init_led,
    output usb_reset_led,

    input usb_init,
    input usb_reset,
    output  pclk,
    output [1:0] addr,
    inout [31:0] dio,
    input flaga,
    (*DONT_TOUCH = "yes"*)input flagb,
    input flagc,
    (*DONT_TOUCH = "yes"*)input flagd,

    output slcs_n,
    output slrd_n,
    output sloe_n,
    output slwr_n,
    output pktend_n
    );

    wire clk_usb;
    wire rst_usb;
    wire reset_n;

    wire [31:0] din;
    wire [31:0] dout;
    wire dout_t;

    wire pll_locked;

    assign rst_usb = ~rst_n || ~pll_locked;
    assign usb_init_led = usb_init;
    assign usb_reset_led = usb_reset;


    (* MARK_DEBUG = "TRUE" *) reg [31:0] s_axis_tdata = 'h0;
    (* MARK_DEBUG = "TRUE" *) reg s_axis_tvalid = 1'b0;
    (* MARK_DEBUG = "TRUE" *) reg s_axis_tlast = 1'b0;
    (* MARK_DEBUG = "TRUE" *) wire s_axis_tready;

    (*DONT_TOUCH = "yes", MARK_DEBUG = "TRUE"*)reg m_axis_tready = 1'b0;
    (*DONT_TOUCH = "yes", MARK_DEBUG = "TRUE"*) wire [31:0] m_axis_tdata;
    (*DONT_TOUCH = "yes", MARK_DEBUG = "TRUE"*) wire m_axis_tvalid;

    IBUF usb_flagb_inbuf(
        .I(flagb),
        .O(usb_flagb)
        );

    IBUF usb_flagd_inbuf(
        .I(flagd),
        .O(usb_flagd)
        );

    always @(posedge clk_usb) begin
        if(rst_usb) begin
            s_axis_tdata <= 'h0;
            s_axis_tvalid <= 1'b0;
            s_axis_tlast <= 1'b0;
        end
        else begin
            s_axis_tvalid <= 1'b1;
            if( s_axis_tvalid && s_axis_tready) begin
                s_axis_tdata <= s_axis_tdata + 1'b1;

                //if( s_axis_tdata == 'h0000_2000)
                //s_axis_tlast <= 1'b1;
                //else
                //s_axis_tlast <= 1'b0;

            end
        end
    end

    always @(posedge clk_usb) begin
        if(rst_usb) m_axis_tready <= 1'b0;
        else m_axis_tready <= 1'b1;
    end

    clk_gen clk_buf(
        .clk40m_i(clk),
        .reset(~rst_n),
        .locked_o(pll_locked),
        .clk100m_o(clk_usb)
        );



    genvar i;

    generate
        for( i=0; i<32; i=i+1)
            IOBUF ez_usb_data_io(
                .IO(dio[i]),
                .I(dout[i]),
                .T(dout_t),
                .O(din[i])
                );
    endgenerate

    ez_usb_fx3_if ez_usb_fx3(
        //clk and rst
        .clk(clk_usb),
        .rst(rst_usb),

        //ez usb interface
        .ez_usb_pclk(pclk), // usb interface clk
        .ez_usb_reset_n(reset_n),// usb reset

        .ez_usb_addr(addr), // usb fifo addr
        // usb data bus interface
        // dio = dout_t ? dout : din
        .ez_usb_din(din),
        .ez_usb_dout(dout),
        .ez_usb_dout_t(dout_t),

        .ez_usb_flaga(flaga), //addr 00, fpga -> usb, full flag, 1 = not full  0 = full
        .ez_usb_flagb(usb_flagb), //addr 01, usb -> fpga, empty flag, 1 = not empty 0 = empty
        .ez_usb_flagc(flagc), //addr 10, fpga -> usb, full flag, 1 = not full  0 = full
        .ez_usb_flagd(usb_flagd), //addr 11, usb -> fpga, empty flag, 1 = not empty 0 = empty

        .ez_usb_slcs_n(slcs_n), // usb chip select, active low
        .ez_usb_slrd_n(slrd_n), // usb read enable, active low
        .ez_usb_sloe_n(sloe_n), // usb data bus output enable, this enable data from usb -> fpga
        .ez_usb_slwr_n(slwr_n), // usb write enable
        .ez_usb_pktend_n(pktend_n), // fpga -> usb packet end

        //user interface
        //fpga --> usb
        .s_axis_tdata(s_axis_tdata),
        .s_axis_tvalid(s_axis_tvalid),
        .s_axis_tlast(s_axis_tlast),
        .s_axis_tready(s_axis_tready),
        //usb --> fpga
        .m_axis_tready(m_axis_tready),
        .m_axis_tdata(m_axis_tdata),
        .m_axis_tvalid(m_axis_tvalid)
        );

endmodule
