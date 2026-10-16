<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:frmwrk="Corel Framework Data"
                exclude-result-prefixes="frmwrk">
  <xsl:output method="xml" encoding="UTF-8" indent="yes" />

  <frmwrk:uiconfig>
    <frmwrk:applicationInfo userConfiguration="true" />
  </frmwrk:uiconfig>

  <xsl:template match="node()|@*">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*" />
    </xsl:copy>
  </xsl:template>

  <xsl:template match="uiConfig/items">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*" />
      <itemData guid="2ED025F2-6C26-4C11-8B01-1489063BA939"
                type="wpfhost"
                hostedType="Addons\SolYanCorelAI\SolYan.CorelAI.Docker.dll,SolYan.CorelAI.Docker.SolYanAiDocker"
                enable="true" />
    </xsl:copy>
  </xsl:template>

  <xsl:template match="uiConfig/dockers">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*" />
      <dockerData guid="B443AE57-8760-42A4-946B-080458E5162C"
                  userCaption="SolYan AI"
                  wantReturn="true"
                  focusStyle="noThrow">
        <container>
          <item dock="fill" margin="0,0,0,0" guidRef="2ED025F2-6C26-4C11-8B01-1489063BA939" />
        </container>
      </dockerData>
    </xsl:copy>
  </xsl:template>
</xsl:stylesheet>
