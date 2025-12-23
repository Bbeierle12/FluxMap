<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0"
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:wix="http://schemas.microsoft.com/wix/2006/wi">

  <xsl:output method="xml" indent="yes"/>

  <xsl:template match="@*|node()">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()" />
    </xsl:copy>
  </xsl:template>

  <xsl:template match="wix:Component/@Id|wix:File/@Id|wix:Directory/@Id|wix:ComponentRef/@Id">
    <xsl:choose>
      <xsl:when test=".='INSTALLFOLDER'">
        <xsl:attribute name="Id"><xsl:value-of select="."/></xsl:attribute>
      </xsl:when>
      <xsl:otherwise>
        <xsl:attribute name="Id">DESK_<xsl:value-of select="."/></xsl:attribute>
      </xsl:otherwise>
    </xsl:choose>
  </xsl:template>
</xsl:stylesheet>
